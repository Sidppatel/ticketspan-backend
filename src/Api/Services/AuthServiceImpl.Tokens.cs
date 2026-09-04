using Google.Apis.Auth;
using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace TicketSpan.Api.Services;

public sealed partial class AuthServiceImpl
{
    private async Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(string googleToken)
    {
        var googleClientId = configuration["GOOGLE_CLIENT_ID"];
        if (string.IsNullOrWhiteSpace(googleClientId))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Google sign-in is not configured"));
        }
        try
        {
            return await GoogleJsonWebSignature.ValidateAsync(googleToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { googleClientId }
            });
        }
        catch (Exception)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid Google token"));
        }
    }

    public override async Task<UserProfile> Me(TicketSpan.Protos.Common.Empty request, ServerCallContext context)
    {
        var tc = context.GetHttpContext().RequestServices.GetRequiredService<TenantContext>();
        if (tc.UsersId is not { } usersId)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));
        }
        return await LoadProfileAsync(usersId, tc, context.CancellationToken);
    }

    public override async Task<UserProfile> UpdateProfile(UpdateProfileRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tc = context.GetHttpContext().RequestServices.GetRequiredService<TenantContext>();
        if (tc.UsersId is not { } usersId)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));
        }
        await using (var connection = await db.OpenAsync(usersId, tc.TenantsId, ct))
        await using (var cmd = new NpgsqlCommand(
            "SELECT sp_update_user_profile(@u, @first, @last, @phone, @addr, @city, @state, @zip, NULL, @bio, @pronouns, @prefs, @billing_addr, @billing_city, @billing_state, @billing_zip)", connection))
        {
            cmd.Parameters.AddWithValue("u", usersId);
            cmd.Parameters.AddWithValue("first", NullIfEmpty(request.FirstName));
            cmd.Parameters.AddWithValue("last", NullIfEmpty(request.LastName));
            cmd.Parameters.AddWithValue("phone", NullIfEmpty(request.Phone));
            cmd.Parameters.AddWithValue("addr", NullIfEmpty(request.AddressLine));
            cmd.Parameters.AddWithValue("city", NullIfEmpty(request.City));
            cmd.Parameters.AddWithValue("state", NullIfEmpty(request.State));
            cmd.Parameters.AddWithValue("zip", NullIfEmpty(request.Zip));
            cmd.Parameters.AddWithValue("bio", NullIfEmpty(request.Bio));
            cmd.Parameters.AddWithValue("pronouns", NullIfEmpty(request.Pronouns));
            cmd.Parameters.Add(new NpgsqlParameter("prefs", NpgsqlTypes.NpgsqlDbType.Jsonb)
            {
                Value = NullIfEmpty(request.PreferencesJson)
            });
            cmd.Parameters.AddWithValue("billing_addr", NullIfEmpty(request.BillingAddressLine));
            cmd.Parameters.AddWithValue("billing_city", NullIfEmpty(request.BillingCity));
            cmd.Parameters.AddWithValue("billing_state", NullIfEmpty(request.BillingState));
            cmd.Parameters.AddWithValue("billing_zip", NullIfEmpty(request.BillingZip));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return await LoadProfileAsync(usersId, tc, ct);
    }

    public override async Task<UserProfile> SetAvatar(SetAvatarRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tc = context.GetHttpContext().RequestServices.GetRequiredService<TenantContext>();
        if (tc.UsersId is not { } usersId)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));
        }
        Guid? imageId = null;
        if (!string.IsNullOrWhiteSpace(request.ImagesId))
        {
            if (!Guid.TryParse(request.ImagesId, out var parsed))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid image id"));
            }
            imageId = parsed;
        }
        await using (var connection = await db.OpenAsync(usersId, tc.TenantsId, ct))
        await using (var cmd = new NpgsqlCommand("SELECT sp_set_user_image(@u, @img)", connection))
        {
            cmd.Parameters.AddWithValue("u", usersId);
            cmd.Parameters.AddWithValue("img", (object?)imageId ?? DBNull.Value);
            await cmd.ExecuteScalarAsync(ct);
        }
        return await LoadProfileAsync(usersId, tc, ct);
    }

    public override async Task<UserProfile> LinkGoogle(LinkGoogleRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tc = context.GetHttpContext().RequestServices.GetRequiredService<TenantContext>();
        if (tc.UsersId is not { } usersId)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));
        }
        var payload = await ValidateGoogleTokenAsync(request.GoogleToken);
        try
        {
            await using var connection = await db.OpenAsync(usersId, tc.TenantsId, ct);
            await using var cmd = new NpgsqlCommand("SELECT users_id FROM sp_link_google(@u, @sub)", connection);
            cmd.Parameters.AddWithValue("u", usersId);
            cmd.Parameters.AddWithValue("sub", payload.Subject);
            await cmd.ExecuteScalarAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, ex.MessageText));
        }
        return await LoadProfileAsync(usersId, tc, ct);
    }

    public override async Task<UserProfile> UnlinkGoogle(TicketSpan.Protos.Common.Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tc = context.GetHttpContext().RequestServices.GetRequiredService<TenantContext>();
        if (tc.UsersId is not { } usersId)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));
        }
        try
        {
            await using var connection = await db.OpenAsync(usersId, tc.TenantsId, ct);
            await using var cmd = new NpgsqlCommand("SELECT users_id FROM sp_unlink_google(@u)", connection);
            cmd.Parameters.AddWithValue("u", usersId);
            await cmd.ExecuteScalarAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0002")
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText));
        }
        return await LoadProfileAsync(usersId, tc, ct);
    }

    private async Task<UserProfile> LoadProfileAsync(Guid usersId, TenantContext tc, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(usersId, tc.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT email, first_name, last_name, email_verified, COALESCE(phone, ''), images_id, "
            + "COALESCE(address_line1, ''), COALESCE(city, ''), COALESCE(state, ''), COALESCE(zip_code, ''), "
            + "google_connected, "
            + "COALESCE(bio, ''), COALESCE(pronouns, ''), COALESCE(preferences_json, ''), "
            + "COALESCE(billing_address_line, ''), COALESCE(billing_city, ''), COALESCE(billing_state, ''), COALESCE(billing_zip, '') "
            + "FROM vw_user_profile WHERE users_id = @id", connection);
        cmd.Parameters.AddWithValue("id", usersId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
        }
        var imagesId = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5);
        var baseUrl = configuration["PUBLIC_BASE_URL"] ?? string.Empty;
        return new UserProfile
        {
            UsersId = usersId.ToString(),
            TenantsId = tc.TenantsId?.ToString() ?? string.Empty,
            Role = tc.Role,
            TenantSlug = tc.TenantSlug,
            Email = reader.GetString(0),
            FirstName = reader.GetString(1),
            LastName = reader.GetString(2),
            EmailVerified = reader.GetBoolean(3),
            Phone = reader.GetString(4),
            AvatarUrl = imagesId is { } img ? $"{baseUrl}/images/{img}" : string.Empty,
            AddressLine = reader.GetString(6),
            City = reader.GetString(7),
            State = reader.GetString(8),
            Zip = reader.GetString(9),
            GoogleConnected = reader.GetBoolean(10),
            Bio = reader.GetString(11),
            Pronouns = reader.GetString(12),
            PreferencesJson = reader.GetString(13),
            BillingAddressLine = reader.GetString(14),
            BillingCity = reader.GetString(15),
            BillingState = reader.GetString(16),
            BillingZip = reader.GetString(17)
        };
    }

    private static object NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    public override async Task<AuthResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
    {
        var rawToken = request.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            var httpContext = context.GetHttpContext();
            if (httpContext != null && httpContext.Request.Cookies.TryGetValue("ts_refresh", out var cookieToken))
            {
                rawToken = cookieToken;
            }
        }
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "No refresh token provided"));
        }

        var ct = context.CancellationToken;
        var v = jwt.ValidationParameters;
        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler { MapInboundClaims = false };
        var parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = v.Issuer,
            ValidAudience = v.Audience,
            IssuerSigningKey = v.Key
        };
        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            var tokenValidationResult = await handler.ValidateTokenAsync(rawToken, parameters);
            if (!tokenValidationResult.IsValid)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid refresh token"));
            }
            principal = new System.Security.Claims.ClaimsPrincipal(tokenValidationResult.ClaimsIdentity);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid refresh token"));
        }
        if (principal.FindFirst("typ")?.Value != "refresh")
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid refresh token"));
        }
        var sub = principal.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var usersId))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid refresh token"));
        }
        var email = principal.FindFirst("email")?.Value ?? string.Empty;
        var slug = principal.FindFirst("tenant_slug")?.Value ?? string.Empty;
        Guid? tokenTenant = Guid.TryParse(principal.FindFirst("tenants_id")?.Value, out var t) ? t : null;
        int tokenVersion = int.TryParse(principal.FindFirst("v")?.Value, out var tv) ? tv : 1;

        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT tenants_id, role, email, is_active, token_version FROM sp_get_user_by_email_hash(@h) "
            + "WHERE users_id = @u AND tenants_id IS NOT DISTINCT FROM @tenant LIMIT 1", connection);
        cmd.Parameters.AddWithValue("h", EmailHasher.Hash(email));
        cmd.Parameters.AddWithValue("u", usersId);
        cmd.Parameters.AddWithValue("tenant", (object?)tokenTenant ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid refresh token"));
        }
        if (!reader.GetBoolean(3))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Account disabled"));
        }
        var currentTokenVersion = reader.GetInt32(4);
        if (tokenVersion != currentTokenVersion)
        {
            ClearRefreshTokenCookie(context);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Session has been invalidated or expired"));
        }
        var rowTenant = reader.IsDBNull(0) ? (Guid?)null : reader.GetGuid(0);
        var role = reader.GetInt16(1);
        var freshEmail = reader.GetString(2);
        var profile = new UserProfile
        {
            UsersId = usersId.ToString(),
            TenantsId = rowTenant?.ToString() ?? string.Empty,
            Email = freshEmail,
            Role = role,
            TenantSlug = slug
        };
        return BuildAuth(usersId, freshEmail, rowTenant, role, slug, profile, currentTokenVersion, context);
    }

    public override async Task<TicketSpan.Protos.Common.AckResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        ClearRefreshTokenCookie(context);
        if (!string.IsNullOrEmpty(request.SessionHash))
        {
            await using var connection = await db.OpenAsync(null, null, ct);
            await using var cmd = new NpgsqlCommand("SELECT sp_revoke_device_session(@h)", connection);
            cmd.Parameters.AddWithValue("h", request.SessionHash);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return new TicketSpan.Protos.Common.AckResponse { Success = true, Message = "Logged out" };
    }

    private void SetRefreshTokenCookie(ServerCallContext context, string refreshToken)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext == null)
        {
            return;
        }
        var host = httpContext.Request.Host.Host;
        var corsBaseDomain = configuration["CORS_BASE_DOMAIN"];
        string? domain = null;
        if (!string.IsNullOrEmpty(corsBaseDomain) && !corsBaseDomain.Contains("localhost") && (host == corsBaseDomain || host.EndsWith("." + corsBaseDomain)))
        {
            domain = "." + corsBaseDomain.TrimStart('.');
        }
        var isHttps = httpContext.Request.IsHttps;
        httpContext.Response.Cookies.Append("ts_refresh", refreshToken, new Microsoft.AspNetCore.Http.CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            Domain = domain,
            Expires = DateTimeOffset.UtcNow.AddMinutes(jwt.RefreshLifetimeMinutes),
            Path = "/"
        });
    }

    private void ClearRefreshTokenCookie(ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext == null)
        {
            return;
        }
        var host = httpContext.Request.Host.Host;
        var corsBaseDomain = configuration["CORS_BASE_DOMAIN"];
        string? domain = null;
        if (!string.IsNullOrEmpty(corsBaseDomain) && !corsBaseDomain.Contains("localhost") && (host == corsBaseDomain || host.EndsWith("." + corsBaseDomain)))
        {
            domain = "." + corsBaseDomain.TrimStart('.');
        }
        var isHttps = httpContext.Request.IsHttps;
        var options = new Microsoft.AspNetCore.Http.CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            Domain = domain,
            Path = "/"
        };
        httpContext.Response.Cookies.Delete("ts_refresh", options);
        if (domain != null)
        {
            httpContext.Response.Cookies.Delete("ts_refresh", new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Path = "/"
            });
        }
    }

    private AuthResponse BuildAuth(Guid usersId, string email, Guid? tenantsId, int role, string slug, UserProfile profile, int tokenVersion = 1, ServerCallContext? context = null)
    {
        var (access, expiresAt) = jwt.Issue(usersId, email, tenantsId, role, slug, tokenVersion);
        var (refresh, _) = jwt.IssueRefresh(usersId, email, tenantsId, role, slug, tokenVersion);
        if (context != null)
        {
            SetRefreshTokenCookie(context, refresh);
        }
        return new AuthResponse { AccessToken = access, RefreshToken = refresh, ExpiresAt = expiresAt, User = profile };
    }

    private static async Task<(Guid usersId, Guid? tenantsId, short role, string firstName, string lastName, bool emailVerified)> CreateAttendeeAsync(
        NpgsqlConnection connection, Guid? tenantsId, string email, string emailHash, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT users_id, tenants_id, role, first_name, last_name, email_verified "
            + "FROM sp_signup_attendee(NULL, @email, @h, @first, @last, NULL)", connection);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("h", emailHash);
        cmd.Parameters.AddWithValue("first", string.Empty);
        cmd.Parameters.AddWithValue("last", string.Empty);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (
            reader.GetGuid(0),
            reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1),
            reader.GetInt16(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetBoolean(5));
    }

    private static short[] GoogleSignInRolesForPortal(string portal) => portal switch
    {
        "admin" => [Lookups.UserRoles.Admin, Lookups.UserRoles.SubTenant, Lookups.UserRoles.Developer],
        "staff" => [Lookups.UserRoles.Staff, Lookups.UserRoles.Admin, Lookups.UserRoles.SubTenant, Lookups.UserRoles.Developer],
        "developer" => [Lookups.UserRoles.Developer],
        _ => [Lookups.UserRoles.PublicViewer]
    };

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

    private static void EnsurePortalAllowsRole(string portal, int role)
    {
        if (!PortalAllowsRole(portal, role))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied,
                "This account cannot sign in on this portal. Use the correct portal, or sign up for an account here."));
        }
    }

    private async Task<Guid?> ResolveTenantAsync(string slug, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(null, null, ct);
        return await ResolveTenantAsync(slug, connection, ct);
    }

    private async Task<Guid?> ResolveTenantAsync(string slug, NpgsqlConnection connection, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return null;
        }
        await using var cmd = new NpgsqlCommand("SELECT tenants_id FROM sp_public_tenant_identity() WHERE slug = @s AND archived_at IS NULL", connection);
        cmd.Parameters.AddWithValue("s", slug);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private async Task<string?> ResolveSlugAsync(Guid tenantsId, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(null, null, ct);
        return await ResolveSlugAsync(tenantsId, connection, ct);
    }

    private async Task<string?> ResolveSlugAsync(Guid tenantsId, NpgsqlConnection connection, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT slug FROM sp_public_tenant_identity() WHERE tenants_id = @id AND archived_at IS NULL", connection);
        cmd.Parameters.AddWithValue("id", tenantsId);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    private async Task MaybeRehashAsync(Guid usersId, string password, string storedHash, short pepperVersion, NpgsqlConnection connection, CancellationToken ct)
    {
        if (!passwordHasher.NeedsRehash(storedHash, pepperVersion))
        {
            return;
        }
        var newHash = await passwordHasher.HashAsync(password);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_user_password(@u, @h, @pv, false, NULL)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        cmd.Parameters.AddWithValue("h", newHash);
        cmd.Parameters.AddWithValue("pv", passwordHasher.CurrentVersion);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task UpdateLastLoginAsync(Guid usersId, NpgsqlConnection connection, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT sp_update_user_last_login(@u)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
