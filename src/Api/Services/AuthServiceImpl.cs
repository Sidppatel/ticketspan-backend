using Google.Apis.Auth;
using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Email;
using Svyne.Api.Security;
using Svyne.Protos.Auth;

namespace Svyne.Api.Services;

public sealed class AuthServiceImpl : AuthService.AuthServiceBase
{
    private readonly Db db;
    private readonly PasswordHasher passwordHasher;
    private readonly JwtTokenService jwt;
    private readonly IConfiguration configuration;
    private readonly IEmailService email;
    private readonly EmailTemplateRenderer templates;
    private readonly AppSettingsProvider settings;
    private readonly ILogger<AuthServiceImpl> logger;

    public AuthServiceImpl(Db db, PasswordHasher passwordHasher, JwtTokenService jwt,
        IConfiguration configuration, IEmailService email, EmailTemplateRenderer templates,
        AppSettingsProvider settings, ILogger<AuthServiceImpl> logger)
    {
        this.db = db;
        this.passwordHasher = passwordHasher;
        this.jwt = jwt;
        this.configuration = configuration;
        this.email = email;
        this.templates = templates;
        this.settings = settings;
        this.logger = logger;
    }

    public override async Task<AuthResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var emailHash = EmailHasher.Hash(request.Email);
        var tenantsId = await ResolveTenantAsync(request.TenantSlug, ct);

        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT users_id, tenants_id, password_hash, pepper_version, role, email, first_name, last_name, email_verified, is_active "
            + "FROM sp_get_user_by_email_for_signin(@h)", connection);
        cmd.Parameters.AddWithValue("h", emailHash);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var rowTenant = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
            var role = reader.GetInt16(4);
            var matchesTenant = role == 99 ? rowTenant is null : rowTenant == tenantsId;
            if (!matchesTenant)
            {
                continue;
            }
            if (reader.IsDBNull(2))
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "Account uses Google sign-in"));
            }
            var usersId = reader.GetGuid(0);
            var storedHash = reader.GetString(2);
            var pepperVersion = reader.GetInt16(3);
            if (!reader.GetBoolean(9))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Account disabled"));
            }
            if (!passwordHasher.Verify(request.Password, storedHash, pepperVersion))
            {
                break;
            }
            EnsurePortalAllowsRole(request.Portal, role);
            var profile = new UserProfile
            {
                UsersId = usersId.ToString(),
                TenantsId = rowTenant?.ToString() ?? string.Empty,
                Email = reader.GetString(5),
                FirstName = reader.GetString(6),
                LastName = reader.GetString(7),
                Role = role,
                TenantSlug = request.TenantSlug,
                EmailVerified = reader.GetBoolean(8)
            };
            await reader.CloseAsync();
            await MaybeRehashAsync(usersId, request.Password, pepperVersion, ct);
            await UpdateLastLoginAsync(usersId, ct);
            return BuildAuth(usersId, profile.Email, rowTenant, role, request.TenantSlug, profile);
        }
        throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid credentials"));
    }

    public override async Task<AuthResponse> SignUp(SignUpRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = await ResolveTenantAsync(request.TenantSlug, ct);
        if (tenantsId is not { } tenant)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown tenant"));
        }
        var emailHash = EmailHasher.Hash(request.Email);
        var passwordHash = passwordHasher.Hash(request.Password);

        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT users_id, role, email, first_name, last_name, email_verified "
            + "FROM sp_signup_attendee(@t, @email, @h, @first, @last, @pwd)", connection);
        cmd.Parameters.AddWithValue("t", tenant);
        cmd.Parameters.AddWithValue("email", request.Email);
        cmd.Parameters.AddWithValue("h", emailHash);
        cmd.Parameters.AddWithValue("first", request.FirstName ?? string.Empty);
        cmd.Parameters.AddWithValue("last", request.LastName ?? string.Empty);
        cmd.Parameters.AddWithValue("pwd", passwordHash);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.Internal, "Sign-up failed"));
            }
            var usersId = reader.GetGuid(0);
            var role = reader.GetInt16(1);
            var profile = new UserProfile
            {
                UsersId = usersId.ToString(),
                TenantsId = tenant.ToString(),
                Email = reader.GetString(2),
                FirstName = reader.GetString(3),
                LastName = reader.GetString(4),
                Role = role,
                TenantSlug = request.TenantSlug,
                EmailVerified = reader.GetBoolean(5)
            };
            return BuildAuth(usersId, profile.Email, tenant, role, request.TenantSlug, profile);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, "An account with this email already exists for this tenant"));
        }
    }

    public override async Task<AuthResponse> GoogleSignIn(GoogleSignInRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.GoogleToken);
        }
        catch (InvalidJwtException)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid Google token"));
        }

        var tenantsId = await ResolveTenantAsync(request.TenantSlug, ct);
        if (tenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown tenant"));
        }
        var emailHash = EmailHasher.Hash(payload.Email);

        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT users_id, tenants_id, role, email, first_name, last_name, email_verified "
            + "FROM sp_signin_user_google(@t, @sub, @email, @h, @first, @last, @role)", connection);
        cmd.Parameters.AddWithValue("t", (object?)tenantsId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sub", payload.Subject);
        cmd.Parameters.AddWithValue("email", payload.Email);
        cmd.Parameters.AddWithValue("h", emailHash);
        cmd.Parameters.AddWithValue("first", payload.GivenName ?? string.Empty);
        cmd.Parameters.AddWithValue("last", payload.FamilyName ?? string.Empty);
        cmd.Parameters.AddWithValue("role", (short)0);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.Internal, "Sign-in failed"));
        }
        var usersId = reader.GetGuid(0);
        var rowTenant = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
        var role = reader.GetInt16(2);
        var profile = new UserProfile
        {
            UsersId = usersId.ToString(),
            TenantsId = rowTenant?.ToString() ?? string.Empty,
            Email = reader.GetString(3),
            FirstName = reader.GetString(4),
            LastName = reader.GetString(5),
            Role = role,
            TenantSlug = request.TenantSlug,
            EmailVerified = reader.GetBoolean(6)
        };
        EnsurePortalAllowsRole(request.Portal, role);
        return BuildAuth(usersId, profile.Email, rowTenant, role, request.TenantSlug, profile);
    }

    public override async Task<UserProfile> Me(Svyne.Protos.Common.Empty request, ServerCallContext context)
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
            "SELECT sp_update_user_profile(@u, @first, @last, @phone, @addr, @city, @state, @zip, NULL)", connection))
        {
            cmd.Parameters.AddWithValue("u", usersId);
            cmd.Parameters.AddWithValue("first", NullIfEmpty(request.FirstName));
            cmd.Parameters.AddWithValue("last", NullIfEmpty(request.LastName));
            cmd.Parameters.AddWithValue("phone", NullIfEmpty(request.Phone));
            cmd.Parameters.AddWithValue("addr", NullIfEmpty(request.AddressLine));
            cmd.Parameters.AddWithValue("city", NullIfEmpty(request.City));
            cmd.Parameters.AddWithValue("state", NullIfEmpty(request.State));
            cmd.Parameters.AddWithValue("zip", NullIfEmpty(request.Zip));
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
        if (!Guid.TryParse(request.ImagesId, out var imageId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid image id"));
        }
        await using (var connection = await db.OpenAsync(usersId, tc.TenantsId, ct))
        await using (var cmd = new NpgsqlCommand("SELECT sp_set_user_image(@u, @img)", connection))
        {
            cmd.Parameters.AddWithValue("u", usersId);
            cmd.Parameters.AddWithValue("img", imageId);
            await cmd.ExecuteScalarAsync(ct);
        }
        return await LoadProfileAsync(usersId, tc, ct);
    }

    private async Task<UserProfile> LoadProfileAsync(Guid usersId, TenantContext tc, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(usersId, tc.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT u.email, u.first_name, u.last_name, u.email_verified, COALESCE(u.phone, ''), u.images_id, "
            + "COALESCE(a.line1, ''), COALESCE(a.city, ''), COALESCE(a.state, ''), COALESCE(a.zip_code, '') "
            + "FROM users u LEFT JOIN addresses a ON a.addresses_id = u.addresses_id WHERE u.users_id = @id", connection);
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
            Zip = reader.GetString(9)
        };
    }

    private static object NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    public override async Task<Svyne.Protos.Common.AckResponse> RequestMagicLink(MagicLinkRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = await ResolveTenantAsync(request.TenantSlug, ct);
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = EmailHasher.Hash(token);
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_create_magic_link(@email, @hash, @exp, @t)", connection);
        cmd.Parameters.AddWithValue("email", request.Email);
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddMinutes(15));
        cmd.Parameters.AddWithValue("t", (object?)tenantsId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return new Svyne.Protos.Common.AckResponse { Success = true, Message = "Magic link sent" };
    }

    public override async Task<AuthResponse> VerifyMagicLink(MagicLinkVerifyRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var hash = EmailHasher.Hash(request.Token);
        string email;
        Guid? linkTenant;
        await using (var connection = await db.OpenAsync(null, null, ct))
        await using (var cmd = new NpgsqlCommand("SELECT email, tenants_id FROM sp_consume_magic_link(@h)", connection))
        {
            cmd.Parameters.AddWithValue("h", hash);
            await using var consumeReader = await cmd.ExecuteReaderAsync(ct);
            if (!await consumeReader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired link"));
            }
            email = consumeReader.GetString(0);
            linkTenant = consumeReader.IsDBNull(1) ? (Guid?)null : consumeReader.GetGuid(1);
        }
        var emailHash = EmailHasher.Hash(email);
        await using var conn = await db.OpenAsync(null, null, ct);
        await using var lookup = new NpgsqlCommand(
            "SELECT users_id, tenants_id, role, email, first_name, last_name, email_verified "
            + "FROM sp_get_user_by_email_hash(@h) WHERE is_active = true AND tenants_id IS NOT DISTINCT FROM @tenant LIMIT 1", conn);
        lookup.Parameters.AddWithValue("h", emailHash);
        lookup.Parameters.AddWithValue("tenant", (object?)linkTenant ?? DBNull.Value);

        Guid usersId;
        Guid? rowTenant;
        short role;
        string firstName;
        string lastName;
        bool emailVerified;
        await using (var reader = await lookup.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                usersId = reader.GetGuid(0);
                rowTenant = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
                role = reader.GetInt16(2);
                firstName = reader.GetString(4);
                lastName = reader.GetString(5);
                emailVerified = reader.GetBoolean(6);
            }
            else if (linkTenant is { } tenant)
            {
                await reader.CloseAsync();
                (usersId, rowTenant, role, firstName, lastName, emailVerified) =
                    await CreateAttendeeAsync(conn, tenant, email, emailHash, ct);
            }
            else
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
            }
        }

        var profile = new UserProfile
        {
            UsersId = usersId.ToString(),
            TenantsId = rowTenant?.ToString() ?? string.Empty,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            EmailVerified = emailVerified
        };
        return BuildAuth(usersId, email, rowTenant, role, string.Empty, profile);
    }

    public override async Task<Svyne.Protos.Common.AckResponse> RequestPasswordReset(PasswordResetRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var emailHash = EmailHasher.Hash(request.Email);
        await using var connection = await db.OpenAsync(null, null, ct);
        Guid usersId;
        await using (var lookup = new NpgsqlCommand("SELECT users_id FROM sp_get_user_by_email_hash(@h) LIMIT 1", connection))
        {
            lookup.Parameters.AddWithValue("h", emailHash);
            var result = await lookup.ExecuteScalarAsync(ct);
            if (result is not Guid u)
            {
                return new Svyne.Protos.Common.AckResponse { Success = true, Message = "If the account exists, a reset link was sent" };
            }
            usersId = u;
        }
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = EmailHasher.Hash(token);
        var expiryHours = await settings.GetIntAsync("password_reset_expiry_hours", 1, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_create_password_reset_token(@u, @h, @exp, @email, NULL)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddHours(expiryHours));
        cmd.Parameters.AddWithValue("email", request.Email);
        await cmd.ExecuteNonQueryAsync(ct);

        // Local dev writes the email as .html to LOCAL_EMAIL_DIR instead of sending.
        // Best-effort: never leak delivery state to the caller (avoids account enumeration).
        try
        {
            var fromAddress = await settings.GetStringAsync("password_reset_email", "noreply@svyne.com", ct);
            var subject = await settings.GetStringAsync("password_reset_subject", "Reset your Svyne password", ct);
            // Prefer the caller's portal origin so the reset link returns to the same
            // host the user requested it from (admin/staff portal or tenant subdomain).
            // Fall back to the configured {slug} template when no origin is supplied.
            string resetBase;
            if (!string.IsNullOrEmpty(request.Origin))
            {
                resetBase = $"{request.Origin.TrimEnd('/')}/set-password";
            }
            else
            {
                var template = await settings.GetStringAsync("password_reset_link_base", "http://{slug}.localhost:5173/set-password", ct);
                resetBase = string.IsNullOrEmpty(request.TenantSlug)
                    ? template.Replace("{slug}.", string.Empty).Replace("{slug}", string.Empty)
                    : template.Replace("{slug}", request.TenantSlug);
            }
            var separator = resetBase.Contains('?') ? "&" : "?";
            var resetLink = $"{resetBase}{separator}token={token}";
            // Shared portals (admin/staff host) need the tenant slug to resolve the
            // tenant at login. Tenant subdomains carry it in the host already.
            if (!string.IsNullOrEmpty(request.TenantSlug))
            {
                resetLink += $"&tenant={Uri.EscapeDataString(request.TenantSlug)}";
            }
            var values = new Dictionary<string, string>
            {
                ["Subject"] = subject,
                ["Email"] = request.Email,
                ["ResetLink"] = resetLink,
                ["ExpiryHours"] = expiryHours.ToString()
            };
            var htmlBody = await templates.RenderAsync("password_reset.html", values, ct);
            await email.SendAsync(fromAddress, request.Email, subject, htmlBody, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email");
        }

        return new Svyne.Protos.Common.AckResponse { Success = true, Message = "If the account exists, a reset link was sent" };
    }

    public override async Task<Svyne.Protos.Common.AckResponse> SetPassword(SetPasswordRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var hash = EmailHasher.Hash(request.Token);
        await using var connection = await db.OpenAsync(null, null, ct);
        Guid usersId;
        await using (var consume = new NpgsqlCommand("SELECT users_id FROM sp_consume_password_reset_token(@h)", connection))
        {
            consume.Parameters.AddWithValue("h", hash);
            var result = await consume.ExecuteScalarAsync(ct);
            if (result is not Guid u)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired token"));
            }
            usersId = u;
        }
        var newHash = passwordHasher.Hash(request.NewPassword);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_user_password(@u, @h, @pv, true, NULL)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        cmd.Parameters.AddWithValue("h", newHash);
        cmd.Parameters.AddWithValue("pv", passwordHasher.CurrentVersion);
        await cmd.ExecuteNonQueryAsync(ct);
        return new Svyne.Protos.Common.AckResponse { Success = true, Message = "Password updated" };
    }

    public override Task<AuthResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
    {
        var v = jwt.ValidationParameters;
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler { MapInboundClaims = false };
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
            principal = handler.ValidateToken(request.RefreshToken, parameters, out _);
        }
        catch (Exception)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid refresh token"));
        }
        var sub = principal.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var usersId))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid refresh token"));
        }
        var email = principal.FindFirst("email")?.Value ?? string.Empty;
        var role = int.TryParse(principal.FindFirst("role")?.Value, out var r) ? r : 0;
        var slug = principal.FindFirst("tenant_slug")?.Value ?? string.Empty;
        Guid? tenantsId = Guid.TryParse(principal.FindFirst("tenants_id")?.Value, out var t) ? t : null;
        var profile = new UserProfile
        {
            UsersId = usersId.ToString(),
            TenantsId = tenantsId?.ToString() ?? string.Empty,
            Email = email,
            Role = role,
            TenantSlug = slug
        };
        return Task.FromResult(BuildAuth(usersId, email, tenantsId, role, slug, profile));
    }

    public override async Task<Svyne.Protos.Common.AckResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_revoke_device_session(@h)", connection);
        cmd.Parameters.AddWithValue("h", request.SessionHash);
        await cmd.ExecuteNonQueryAsync(ct);
        return new Svyne.Protos.Common.AckResponse { Success = true, Message = "Logged out" };
    }

    private AuthResponse BuildAuth(Guid usersId, string email, Guid? tenantsId, int role, string slug, UserProfile profile)
    {
        var (token, expiresAt) = jwt.Issue(usersId, email, tenantsId, role, slug);
        return new AuthResponse { AccessToken = token, RefreshToken = token, ExpiresAt = expiresAt, User = profile };
    }

    private static async Task<(Guid usersId, Guid? tenantsId, short role, string firstName, string lastName, bool emailVerified)> CreateAttendeeAsync(
        NpgsqlConnection connection, Guid tenantsId, string email, string emailHash, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT users_id, tenants_id, role, first_name, last_name, email_verified "
            + "FROM sp_signup_attendee(@t, @email, @h, @first, @last, NULL)", connection);
        cmd.Parameters.AddWithValue("t", tenantsId);
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

    // Enforce that the account's role is allowed on the portal it's logging into.
    // Empty portal = no restriction (back-compat for non-portal callers).
    // Roles: Attendee=0, Admin=1, Staff=2, SubTenant=3, Developer=99.
    private static void EnsurePortalAllowsRole(string portal, int role)
    {
        if (string.IsNullOrEmpty(portal))
        {
            return;
        }
        var allowed = portal switch
        {
            "public" => role == 0,
            "admin" => role == 1 || role == 3,
            "staff" => role == 2,
            "developer" => role == 99,
            _ => true
        };
        if (!allowed)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied,
                "This account cannot sign in on this portal. Use the correct portal, or sign up for an account here."));
        }
    }

    private async Task<Guid?> ResolveTenantAsync(string slug, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return null;
        }
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT tenants_id FROM tenants WHERE slug = @s AND archived_at IS NULL", connection);
        cmd.Parameters.AddWithValue("s", slug);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private async Task MaybeRehashAsync(Guid usersId, string password, short pepperVersion, CancellationToken ct)
    {
        if (!passwordHasher.NeedsRehash(pepperVersion))
        {
            return;
        }
        var newHash = passwordHasher.Hash(password);
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_user_password(@u, @h, @pv, false, NULL)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        cmd.Parameters.AddWithValue("h", newHash);
        cmd.Parameters.AddWithValue("pv", passwordHasher.CurrentVersion);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task UpdateLastLoginAsync(Guid usersId, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_update_user_last_login(@u)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
