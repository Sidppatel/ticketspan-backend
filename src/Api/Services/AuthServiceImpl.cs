using Google.Apis.Auth;
using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Auth;

namespace Svyne.Api.Services;

public sealed class AuthServiceImpl : AuthService.AuthServiceBase
{
    private readonly Db db;
    private readonly PasswordHasher passwordHasher;
    private readonly JwtTokenService jwt;
    private readonly IConfiguration configuration;

    public AuthServiceImpl(Db db, PasswordHasher passwordHasher, JwtTokenService jwt, IConfiguration configuration)
    {
        this.db = db;
        this.passwordHasher = passwordHasher;
        this.jwt = jwt;
        this.configuration = configuration;
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
        return BuildAuth(usersId, profile.Email, rowTenant, role, request.TenantSlug, profile);
    }

    public override Task<UserProfile> Me(Svyne.Protos.Common.Empty request, ServerCallContext context)
    {
        var tc = context.GetHttpContext().RequestServices.GetRequiredService<TenantContext>();
        if (tc.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));
        }
        return Task.FromResult(new UserProfile
        {
            UsersId = tc.UsersId.ToString(),
            TenantsId = tc.TenantsId?.ToString() ?? string.Empty,
            Role = tc.Role,
            TenantSlug = tc.TenantSlug
        });
    }

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
        await using (var connection = await db.OpenAsync(null, null, ct))
        await using (var cmd = new NpgsqlCommand("SELECT email FROM sp_consume_magic_link(@h)", connection))
        {
            cmd.Parameters.AddWithValue("h", hash);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not string e)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired link"));
            }
            email = e;
        }
        var emailHash = EmailHasher.Hash(email);
        await using var conn = await db.OpenAsync(null, null, ct);
        await using var lookup = new NpgsqlCommand(
            "SELECT users_id, tenants_id, role, email, first_name, last_name, email_verified FROM sp_get_user_by_email_hash(@h) WHERE is_active = true LIMIT 1", conn);
        lookup.Parameters.AddWithValue("h", emailHash);
        await using var reader = await lookup.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
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
            EmailVerified = reader.GetBoolean(6)
        };
        return BuildAuth(usersId, profile.Email, rowTenant, role, string.Empty, profile);
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
        await using var cmd = new NpgsqlCommand("SELECT sp_create_password_reset_token(@u, @h, @exp, @email, NULL)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddHours(1));
        cmd.Parameters.AddWithValue("email", request.Email);
        await cmd.ExecuteNonQueryAsync(ct);
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
