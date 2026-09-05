using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;

namespace TicketSpan.Api.Controllers;

[ApiController]
public sealed class AuthorizationController : ControllerBase
{
    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IOpenIddictScopeManager scopeManager;
    private readonly Db db;
    private readonly PasswordHasher passwordHasher;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        Db db,
        PasswordHasher passwordHasher)
    {
        this.applicationManager = applicationManager;
        this.scopeManager = scopeManager;
        this.db = db;
        this.passwordHasher = passwordHasher;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        Response.Headers.Remove("X-Frame-Options");
        Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self' http://localhost:* http://*.localhost:* https://ticketspan.com https://*.ticketspan.com";

        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            if (string.Equals(request.Prompt, "none", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.LoginRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not logged in."
                    }));
            }

            var returnUrl = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetEncodedUrl(Request);
            var redirectParam = request.RedirectUri;
            string loginBaseUrl = "http://localhost:5173/login";
            if (!string.IsNullOrEmpty(redirectParam) && Uri.TryCreate(redirectParam, UriKind.Absolute, out var rUri))
            {
                var host = rUri.Host;
                var scheme = rUri.Scheme;
                var port = rUri.IsDefaultPort ? "" : $":{rUri.Port}";
                if (host == "localhost" || host.EndsWith(".localhost"))
                {
                    loginBaseUrl = $"{scheme}://localhost{port}/login";
                }
                else if (host.EndsWith("ticketspan.com"))
                {
                    loginBaseUrl = $"{scheme}://ticketspan.com{port}/login";
                }
            }
            return Redirect($"{loginBaseUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}&session_expired=1");
        }

        var principal = result.Principal;
        var sub = principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var usersId))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("ts_sso", new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                SameSite = SameSiteMode.None,
                Secure = true,
            });
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.LoginRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not logged in."
                }));
        }

        var tokenVersionClaim = principal.FindFirst("v")?.Value;
        int.TryParse(tokenVersionClaim, out var tokenVersion);
        var ct = HttpContext.RequestAborted;
        var email = principal.FindFirst(OpenIddictConstants.Claims.Email)?.Value ?? string.Empty;

        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT is_active, token_version FROM vw_user_profile WHERE users_id = @u LIMIT 1", connection);
        cmd.Parameters.AddWithValue("u", usersId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || !reader.GetBoolean(0) || reader.GetInt32(1) != tokenVersion)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("ts_sso", new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                SameSite = SameSiteMode.None,
                Secure = true,
            });
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.LoginRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not logged in."
                }));
        }

        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        foreach (var claim in principal.Claims)
        {
            identity.AddClaim(new Claim(claim.Type, claim.Value));
        }

        identity.SetClaim(OpenIddictConstants.Claims.Subject, usersId.ToString());
        if (!string.IsNullOrEmpty(email))
        {
            identity.SetClaim(OpenIddictConstants.Claims.Email, email);
        }
        identity.SetClaim("v", tokenVersion.ToString());

        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

        SetDestinations(identity);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsPasswordGrantType())
        {
            var email = request.Username ?? string.Empty;
            var password = request.Password ?? string.Empty;
            var portal = (string?)request["portal"] ?? Request.Headers["x-portal"].ToString();
            if (string.IsNullOrWhiteSpace(portal))
            {
                portal = "public";
            }
            var tenantSlug = (string?)request["tenant_slug"] ?? Request.Headers["x-tenant-slug"].ToString();

            var ct = HttpContext.RequestAborted;
            var emailHash = EmailHasher.Hash(email);
            var slugScoped = portal.Length == 0 || portal == "public";

            await using var connection = await db.OpenAsync(null, null, ct);
            var tenantsId = slugScoped ? await ResolveTenantAsync(tenantSlug, connection, ct) : null;

            var viewName = portal switch
            {
                "admin" => "vw_signin_admin",
                "staff" => "vw_signin_staff",
                "developer" => "vw_signin_developer",
                _ => "vw_signin_public"
            };

            await using var cmd = new NpgsqlCommand(
                $"SELECT users_id, tenants_id, password_hash, pepper_version, role, email, first_name, last_name, email_verified, is_active, token_version "
                + $"FROM {viewName} WHERE email_hash = @h", connection);
            cmd.Parameters.AddWithValue("h", emailHash);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            Guid? matchedUserId = null;
            Guid? matchedTenantId = null;
            short matchedRole = 0;
            string matchedEmail = string.Empty;
            string matchedName = string.Empty;
            int matchedTokenVersion = 1;

            while (await reader.ReadAsync(ct))
            {
                var rowTenant = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
                var role = reader.GetInt16(4);

                if (slugScoped)
                {
                    var matchesTenant = (role == Lookups.UserRoles.Developer || role == Lookups.UserRoles.PublicViewer)
                        || rowTenant == tenantsId;
                    if (!matchesTenant)
                    {
                        continue;
                    }
                }

                if (reader.IsDBNull(2))
                {
                    continue;
                }

                var storedHash = reader.GetString(2);
                var pepperVersion = reader.GetInt16(3);
                if (!reader.GetBoolean(9))
                {
                    return CreateErrorResponse(OpenIddictConstants.Errors.AccessDenied, "Account is disabled.");
                }

                if (!await passwordHasher.VerifyAsync(password, storedHash, pepperVersion))
                {
                    continue;
                }

                matchedUserId = reader.GetGuid(0);
                matchedTenantId = rowTenant;
                matchedRole = role;
                matchedEmail = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                var firstName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                var lastName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
                matchedName = $"{firstName} {lastName}".Trim();
                matchedTokenVersion = reader.GetInt32(10);
                break;
            }

            if (matchedUserId is null)
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.InvalidGrant, "Invalid email or password.");
            }

            if (!PortalAllowsRole(portal, matchedRole))
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.AccessDenied, "This account cannot sign in to this portal.");
            }

            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: OpenIddictConstants.Claims.Name,
                roleType: OpenIddictConstants.Claims.Role);

            identity.AddClaim(OpenIddictConstants.Claims.Subject, matchedUserId.Value.ToString());
            identity.AddClaim(OpenIddictConstants.Claims.Email, matchedEmail);
            identity.AddClaim(OpenIddictConstants.Claims.Name, matchedName);
            identity.AddClaim(OpenIddictConstants.Claims.Role, matchedRole.ToString());
            identity.AddClaim("role", matchedRole.ToString());
            identity.AddClaim("tenants_id", matchedTenantId?.ToString() ?? string.Empty);
            identity.AddClaim("tenant_slug", tenantSlug ?? string.Empty);
            identity.AddClaim("v", matchedTokenVersion.ToString());

            identity.SetScopes(request.GetScopes());
            identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

            SetDestinations(identity);

            var cookieIdentity = new ClaimsIdentity(identity.Claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(cookieIdentity));

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType())
        {
            var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!authResult.Succeeded || authResult.Principal is null)
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.InvalidGrant, "Authorization code is invalid.");
            }

            var principal = authResult.Principal;
            var sub = principal.GetClaim(OpenIddictConstants.Claims.Subject)
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sub, out var usersId))
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.InvalidGrant, "Invalid token subject.");
            }

            var ct = HttpContext.RequestAborted;
            await using var connection = await db.OpenAsync(null, null, ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT is_active, token_version FROM vw_user_profile WHERE users_id = @u LIMIT 1", connection);
            cmd.Parameters.AddWithValue("u", usersId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.InvalidGrant, "User not found.");
            }
            if (!reader.GetBoolean(0))
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.AccessDenied, "Account is disabled.");
            }

            var tokenVersionClaim = principal.GetClaim("v") ?? principal.FindFirst("v")?.Value;
            int.TryParse(tokenVersionClaim, out var tokenVersion);
            var currentTokenVersion = reader.GetInt32(1);
            if (tokenVersion != currentTokenVersion)
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.InvalidGrant, "Session has expired or been revoked.");
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsRefreshTokenGrantType())
        {
            var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!authResult.Succeeded || authResult.Principal is null)
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.InvalidGrant, "Refresh token is invalid.");
            }

            var principal = authResult.Principal;
            var sub = principal.GetClaim(OpenIddictConstants.Claims.Subject)
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sub, out var usersId))
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.InvalidGrant, "Invalid token subject.");
            }

            var tokenVersionClaim = principal.GetClaim("v") ?? principal.FindFirst("v")?.Value;
            int.TryParse(tokenVersionClaim, out var tokenVersion);

            var ct = HttpContext.RequestAborted;
            await using var connection = await db.OpenAsync(null, null, ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT is_active, token_version FROM vw_user_profile WHERE users_id = @u LIMIT 1", connection);
            cmd.Parameters.AddWithValue("u", usersId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.InvalidGrant, "User not found.");
            }
            if (!reader.GetBoolean(0))
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.AccessDenied, "Account is disabled.");
            }

            var currentTokenVersion = reader.GetInt32(1);
            if (tokenVersion != currentTokenVersion)
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.InvalidGrant, "Session has expired or been revoked.");
            }

            var roleClaim = principal.GetClaim(OpenIddictConstants.Claims.Role)
                ?? principal.GetClaim("role")
                ?? principal.FindFirst(ClaimTypes.Role)?.Value;
            int.TryParse(roleClaim, out var role);
            var portal = Request.Headers["x-portal"].ToString();
            if (!string.IsNullOrEmpty(portal) && !PortalAllowsRole(portal, role))
            {
                return CreateErrorResponse(OpenIddictConstants.Errors.AccessDenied, "Portal role violation.");
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return CreateErrorResponse(OpenIddictConstants.Errors.UnsupportedGrantType, "The specified grant type is not supported.");
    }

    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Produces("application/json")]
    public IActionResult Userinfo()
    {
        var claims = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [OpenIddictConstants.Claims.Subject] = User.GetClaim(OpenIddictConstants.Claims.Subject),
            [OpenIddictConstants.Claims.Email] = User.GetClaim(OpenIddictConstants.Claims.Email),
            [OpenIddictConstants.Claims.Name] = User.GetClaim(OpenIddictConstants.Claims.Name),
            [OpenIddictConstants.Claims.Role] = User.GetClaim(OpenIddictConstants.Claims.Role),
            ["role"] = User.GetClaim("role"),
            ["tenants_id"] = User.GetClaim("tenants_id"),
            ["tenant_slug"] = User.GetClaim("tenant_slug")
        };

        return Ok(claims);
    }

    [HttpPost("~/connect/logout")]
    [HttpGet("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LogOut()
    {
        var ct = HttpContext.RequestAborted;
        Guid? usersId = null;

        var validationResult = await HttpContext.AuthenticateAsync(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        if (validationResult.Succeeded && validationResult.Principal is { } vp)
        {
            var sub = vp.GetClaim(OpenIddictConstants.Claims.Subject);
            if (Guid.TryParse(sub, out var uid))
            {
                usersId = uid;
            }
        }

        if (usersId is null)
        {
            var serverResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (serverResult.Succeeded && serverResult.Principal is { } sp)
            {
                var sub = sp.GetClaim(OpenIddictConstants.Claims.Subject);
                if (Guid.TryParse(sub, out var uid))
                {
                    usersId = uid;
                }
            }
        }

        if (usersId is null)
        {
            var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (cookieResult.Succeeded && cookieResult.Principal is { } cp)
            {
                var sub = cp.FindFirst(OpenIddictConstants.Claims.Subject)?.Value ?? cp.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var uid))
                {
                    usersId = uid;
                }
            }
        }

        if (usersId is null && Request.HasFormContentType)
        {
            var formUserId = Request.Form["users_id"].ToString();
            if (Guid.TryParse(formUserId, out var fuid))
            {
                usersId = fuid;
            }
            if (usersId is null)
            {
                var tokenStr = Request.Form["id_token_hint"].ToString();
                if (string.IsNullOrEmpty(tokenStr))
                {
                    tokenStr = Request.Form["token"].ToString();
                }
                if (!string.IsNullOrEmpty(tokenStr))
                {
                    usersId = ExtractSubjectFromJwt(tokenStr);
                }
            }
        }

        if (usersId is null && Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerVal = authHeader.ToString();
            if (headerVal.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var tokenStr = headerVal["Bearer ".Length..].Trim();
                usersId = ExtractSubjectFromJwt(tokenStr);
            }
        }

        if (usersId is { } u)
        {
            await using var connection = await db.OpenAsync(null, null, ct);
            await using var cmd = new NpgsqlCommand("SELECT sp_revoke_user_sessions(@u)", connection);
            cmd.Parameters.AddWithValue("u", u);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("ts_sso", new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.None,
            Secure = true,
        });
        Response.Cookies.Delete("ts_sso", new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
        });
        Response.Cookies.Delete("ts_sso", new CookieOptions
        {
            Path = "/",
        });

        return Ok(new { success = true });
    }

    private static Guid? ExtractSubjectFromJwt(string token)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwt = handler.ReadJwtToken(token);
                var sub = jwt.Subject ?? jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var uid))
                {
                    return uid;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static void SetDestinations(ClaimsIdentity identity)
    {
        foreach (var claim in identity.Claims)
        {
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
        }
    }

    private static bool PortalAllowsRole(string portal, int role)
    {
        if (string.IsNullOrEmpty(portal))
        {
            return true;
        }
        return portal switch
        {
            "public" => true,
            "admin" => role == Lookups.UserRoles.Admin || role == Lookups.UserRoles.SubTenant || role == Lookups.UserRoles.Developer,
            "staff" => role == Lookups.UserRoles.Staff || role == Lookups.UserRoles.Admin || role == Lookups.UserRoles.SubTenant || role == Lookups.UserRoles.Developer,
            "developer" => role == Lookups.UserRoles.Developer,
            _ => true
        };
    }

    private static async Task<Guid?> ResolveTenantAsync(string? slug, NpgsqlConnection connection, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return null;
        }
        await using var cmd = new NpgsqlCommand("SELECT tenants_id FROM tenants WHERE slug = @s AND archived_at IS NULL LIMIT 1", connection);
        cmd.Parameters.AddWithValue("s", slug);
        var res = await cmd.ExecuteScalarAsync(ct);
        return res is Guid g ? g : null;
    }

    private IActionResult CreateErrorResponse(string error, string description)
    {
        var properties = new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        });

        return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
