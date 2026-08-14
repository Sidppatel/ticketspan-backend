using Google.Apis.Auth;
using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Auth;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IServiceScopeFactory scopeFactory;

    public AuthServiceImpl(Db db, PasswordHasher passwordHasher, JwtTokenService jwt,
        IConfiguration configuration, IEmailService email, EmailTemplateRenderer templates,
        AppSettingsProvider settings, ILogger<AuthServiceImpl> logger,
        TicketSpan.Api.Storage.ObjectStorage storage, IHttpClientFactory httpFactory,
        IServiceScopeFactory scopeFactory)
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
        this.scopeFactory = scopeFactory;
    }

    public override async Task<AuthResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var emailHash = EmailHasher.Hash(request.Email);
        var portal = request.Portal ?? string.Empty;
        var slugScoped = portal.Length == 0 || portal == "public";

        await using var connection = await db.OpenAsync(null, null, ct);
        var tenantsId = slugScoped ? await ResolveTenantAsync(request.TenantSlug, connection, ct) : null;

        var viewName = portal switch
        {
            "admin" => "vw_signin_admin",
            "staff" => "vw_signin_staff",
            "developer" => "vw_signin_developer",
            _ => "vw_signin_public"
        };

        await using var cmd = new NpgsqlCommand(
            $"SELECT users_id, tenants_id, password_hash, pepper_version, role, email, first_name, last_name, email_verified, is_active "
            + $"FROM {viewName} WHERE email_hash = @h", connection);
        cmd.Parameters.AddWithValue("h", emailHash);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var rowTenant = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
            var role = reader.GetInt16(4);
            
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
            if (!await passwordHasher.VerifyAsync(request.Password, storedHash, pepperVersion))
            {
                continue;
            }
            var email = reader.GetString(5);
            var firstName = reader.GetString(6);
            var lastName = reader.GetString(7);
            var emailVerified = reader.GetBoolean(8);
            await reader.CloseAsync();
            var tenantSlug = rowTenant is { } rt
                ? await ResolveSlugAsync(rt, connection, ct) ?? request.TenantSlug
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

            var pwd = request.Password;
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var backgroundDb = scope.ServiceProvider.GetRequiredService<Db>();
                    await using var bgConnection = await backgroundDb.OpenAsync(null, null, CancellationToken.None);
                    await MaybeRehashAsync(usersId, pwd, pepperVersion, bgConnection, CancellationToken.None);
                    await UpdateLastLoginAsync(usersId, bgConnection, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to execute post-login background tasks for user {UsersId}", usersId);
                }
            });

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

    public override async Task<AuthResponse> GoogleSignIn(GoogleSignInRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var payload = await ValidateGoogleTokenAsync(request.GoogleToken);
        if (payload.EmailVerified != true)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Google account email is not verified"));
        }

        var portal = request.Portal ?? string.Empty;
        var slugScoped = portal.Length == 0 || portal == "public";
        var tenantsId = slugScoped ? await ResolveTenantAsync(request.TenantSlug, ct) : null;
        if (slugScoped && tenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown tenant"));
        }
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
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                "This account is not allowed to use Google sign-in. Sign in with your password, then connect Google from your profile."));
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
        var email = reader.GetString(3);
        var firstName = reader.GetString(4);
        var lastName = reader.GetString(5);
        var emailVerified = reader.GetBoolean(6);
        var hasAvatar = !reader.IsDBNull(7);
        EnsurePortalAllowsRole(portal, role);
        await reader.CloseAsync();
        var tenantSlug = request.TenantSlug;
        if (string.IsNullOrEmpty(tenantSlug) && rowTenant is { } rt)
        {
            tenantSlug = await ResolveSlugAsync(rt, ct) ?? string.Empty;
        }
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
        if (!hasAvatar && !string.IsNullOrWhiteSpace(payload.Picture))
        {
            var pictureUrl = payload.Picture;
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var bgSvc = scope.ServiceProvider.GetRequiredService<AuthServiceImpl>();
                    await bgSvc.TryStoreGoogleAvatarAsync(usersId, rowTenant, pictureUrl, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed background store of Google avatar for {UsersId}", usersId);
                }
            });
        }
        return BuildAuth(usersId, profile.Email, rowTenant, role, tenantSlug, profile);
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
}

