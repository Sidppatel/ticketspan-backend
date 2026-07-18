using Google.Apis.Auth;
using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Auth;

namespace TicketSpan.Api.Services;

public sealed partial class AuthServiceImpl : AuthService.AuthServiceBase
{
    private readonly Db db;
    private readonly PasswordHasher passwordHasher;
    private readonly JwtTokenService jwt;
    private readonly IConfiguration configuration;
    private readonly IEmailService email;
    private readonly EmailTemplateRenderer templates;
    private readonly AppSettingsProvider settings;
    private readonly ILogger<AuthServiceImpl> logger;
    private readonly TicketSpan.Api.Storage.ObjectStorage storage;
    private readonly IHttpClientFactory httpFactory;

    public AuthServiceImpl(Db db, PasswordHasher passwordHasher, JwtTokenService jwt,
        IConfiguration configuration, IEmailService email, EmailTemplateRenderer templates,
        AppSettingsProvider settings, ILogger<AuthServiceImpl> logger,
        TicketSpan.Api.Storage.ObjectStorage storage, IHttpClientFactory httpFactory)
    {
        this.db = db;
        this.passwordHasher = passwordHasher;
        this.jwt = jwt;
        this.configuration = configuration;
        this.email = email;
        this.templates = templates;
        this.settings = settings;
        this.logger = logger;
        this.storage = storage;
        this.httpFactory = httpFactory;
    }

    public override async Task<AuthResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var emailHash = EmailHasher.Hash(request.Email);
        var portal = request.Portal ?? string.Empty;
        var slugScoped = portal.Length == 0 || portal == "public";
        var tenantsId = slugScoped ? await ResolveTenantAsync(request.TenantSlug, ct) : null;

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
            if (!PortalAllowsRole(portal, role))
            {
                continue;
            }
            if (slugScoped)
            {
                var matchesTenant = role == Lookups.UserRoles.Developer ? rowTenant is null : rowTenant == tenantsId;
                if (!matchesTenant)
                {
                    continue;
                }
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
            var email = reader.GetString(5);
            var firstName = reader.GetString(6);
            var lastName = reader.GetString(7);
            var emailVerified = reader.GetBoolean(8);
            await reader.CloseAsync();
            var tenantSlug = rowTenant is { } rt
                ? await ResolveSlugAsync(rt, ct) ?? request.TenantSlug
                : string.Empty;
            var profile = new UserProfile
            {
                UsersId = usersId.ToString(),
                TenantsId = rowTenant?.ToString() ?? string.Empty,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                TenantSlug = tenantSlug,
                EmailVerified = emailVerified
            };
            await MaybeRehashAsync(usersId, request.Password, pepperVersion, ct);
            await UpdateLastLoginAsync(usersId, ct);
            return BuildAuth(usersId, profile.Email, rowTenant, role, tenantSlug, profile);
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
        catch (InvalidJwtException)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid Google token"));
        }
    }

    public override async Task<AuthResponse> GoogleSignIn(GoogleSignInRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var payload = await ValidateGoogleTokenAsync(request.GoogleToken);
        if (payload.EmailVerified != true)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Google account email is not verified"));
        }

        var tenantsId = await ResolveTenantAsync(request.TenantSlug, ct);
        if (tenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown tenant"));
        }
        var portal = request.Portal ?? string.Empty;
        var emailHash = EmailHasher.Hash(payload.Email);

        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT users_id, tenants_id, role, email, first_name, last_name, email_verified, images_id "
            + "FROM sp_signin_user_google(@t, @sub, @email, @h, @first, @last, @role, @allowed)", connection);
        cmd.Parameters.AddWithValue("t", (object?)tenantsId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sub", payload.Subject);
        cmd.Parameters.AddWithValue("email", payload.Email);
        cmd.Parameters.AddWithValue("h", emailHash);
        cmd.Parameters.AddWithValue("first", payload.GivenName ?? string.Empty);
        cmd.Parameters.AddWithValue("last", payload.FamilyName ?? string.Empty);
        cmd.Parameters.AddWithValue("role", (short)Lookups.UserRoles.PublicViewer);
        cmd.Parameters.AddWithValue("allowed", GoogleSignInRolesForPortal(portal));

        NpgsqlDataReader reader;
        try
        {
            reader = await cmd.ExecuteReaderAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText));
        }
        catch (PostgresException ex) when (ex.SqlState == "P0003")
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Account disabled"));
        }
        catch (PostgresException ex) when (ex.SqlState == "P0004")
        {
            throw new RpcException(new Status(StatusCode.NotFound,
                "No account found for this Google account on this portal. Ask your administrator for an invitation."));
        }
        await using (reader)
        {
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
        EnsurePortalAllowsRole(portal, role);
        var hasAvatar = !reader.IsDBNull(7);
        if (!hasAvatar && !string.IsNullOrWhiteSpace(payload.Picture))
        {
            await TryStoreGoogleAvatarAsync(usersId, rowTenant, payload.Picture, ct);
        }
        return BuildAuth(usersId, profile.Email, rowTenant, role, request.TenantSlug, profile);
        }
    }

    private async Task TryStoreGoogleAvatarAsync(Guid usersId, Guid? tenantsId, string pictureUrl, CancellationToken ct)
    {
        try
        {
            var http = httpFactory.CreateClient();
            using var response = await http.GetAsync(pictureUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0)
            {
                return;
            }
            var storageKey = $"user/{Guid.NewGuid():N}.jpg";
            await using (var blob = new MemoryStream(bytes))
            {
                await storage.PutAsync(storageKey, blob, contentType, ct);
            }
            await using var connection = await db.OpenAsync(usersId, tenantsId, ct);
            await using (var img = new NpgsqlCommand(
                "SELECT sp_create_image(@et, @eid, @key, @name, @size, 0, 0, 0, @uid, NULL, NULL, NULL, @ct, NULL, @t)", connection))
            {
                img.Parameters.AddWithValue("et", "user");
                img.Parameters.AddWithValue("eid", usersId);
                img.Parameters.AddWithValue("key", storageKey);
                img.Parameters.AddWithValue("name", "google-avatar.jpg");
                img.Parameters.AddWithValue("size", bytes.Length);
                img.Parameters.AddWithValue("uid", usersId);
                img.Parameters.AddWithValue("ct", contentType);
                img.Parameters.AddWithValue("t", (object?)tenantsId ?? DBNull.Value);
                var imageId = await img.ExecuteScalarAsync(ct);
                if (imageId is Guid imgGuid)
                {
                    await using var link = new NpgsqlCommand("SELECT sp_set_user_image(@u, @img)", connection);
                    link.Parameters.AddWithValue("u", usersId);
                    link.Parameters.AddWithValue("img", imgGuid);
                    await link.ExecuteNonQueryAsync(ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to store Google avatar for user {UsersId}", usersId);
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
            await using var cmd = new NpgsqlCommand("SELECT sp_link_google(@u, @sub)", connection);
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
            await using var cmd = new NpgsqlCommand("SELECT sp_unlink_google(@u)", connection);
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
            + "google_connected "
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
            GoogleConnected = reader.GetBoolean(10)
        };
    }

    private static object NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

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

    public override async Task<TicketSpan.Protos.Common.AckResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_revoke_device_session(@h)", connection);
        cmd.Parameters.AddWithValue("h", request.SessionHash);
        await cmd.ExecuteNonQueryAsync(ct);
        return new TicketSpan.Protos.Common.AckResponse { Success = true, Message = "Logged out" };
    }

    private AuthResponse BuildAuth(Guid usersId, string email, Guid? tenantsId, int role, string slug, UserProfile profile)
    {
        var (access, expiresAt) = jwt.Issue(usersId, email, tenantsId, role, slug);
        var (refresh, _) = jwt.IssueRefresh(usersId, email, tenantsId, role, slug);
        return new AuthResponse { AccessToken = access, RefreshToken = refresh, ExpiresAt = expiresAt, User = profile };
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
        if (string.IsNullOrEmpty(slug))
        {
            return null;
        }
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT tenants_id FROM sp_public_tenant_identity() WHERE slug = @s AND archived_at IS NULL", connection);
        cmd.Parameters.AddWithValue("s", slug);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private async Task<string?> ResolveSlugAsync(Guid tenantsId, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT slug FROM sp_public_tenant_identity() WHERE tenants_id = @id AND archived_at IS NULL", connection);
        cmd.Parameters.AddWithValue("id", tenantsId);
        return await cmd.ExecuteScalarAsync(ct) as string;
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
