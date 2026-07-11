using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Svyne.Api.Data;
using Svyne.Api.Middleware;
using Svyne.Api.Security;
using Svyne.Api.Services;

System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(renderPort))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_HTTP_PORTS", renderPort);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(
    new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());

if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrEmpty(builder.Configuration["JWT_SIGNING_KEY"]))
    {
        throw new InvalidOperationException("JWT_SIGNING_KEY must be set outside Development");
    }
    if (!string.IsNullOrEmpty(builder.Configuration["STRIPE_SECRET_KEY"])
        && string.IsNullOrEmpty(builder.Configuration["STRIPE_WEBHOOK_SECRET"]))
    {
        throw new InvalidOperationException("STRIPE_WEBHOOK_SECRET must be set when Stripe is configured outside Development");
    }
}

var http2Only = builder.Configuration["GRPC_HTTP2_ONLY"] == "true";
if (http2Only)
{
    builder.WebHost.ConfigureKestrel(options =>
        options.ConfigureEndpointDefaults(listen =>
            listen.Protocols = HttpProtocols.Http2));
}

const string CorsPolicy = "frontend";
var corsOrigins = (builder.Configuration["CORS_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var corsBaseDomain = builder.Configuration["CORS_BASE_DOMAIN"];
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (corsOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }
            var host = uri.Host;
            if (host == "localhost" || host.EndsWith(".localhost"))
            {
                return true;
            }
            return !string.IsNullOrEmpty(corsBaseDomain)
                && (host == corsBaseDomain || host.EndsWith("." + corsBaseDomain));
        });
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("grpc-status", "grpc-message", "grpc-status-details-bin");
    }));

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<Svyne.Api.ErrorHandling.ErrorLoggingInterceptor>();
    options.Interceptors.Add<Svyne.Api.Security.EventManagerAuthorizationInterceptor>();
});
builder.Services.AddSingleton<Svyne.Api.ErrorHandling.ErrorLogger>();
builder.Services.AddSingleton<Svyne.Api.ErrorHandling.ErrorLoggingInterceptor>();
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<StartupSeeder>();
builder.Services.AddSingleton<AppSettingsProvider>();
builder.Services.AddSingleton<Svyne.Api.Email.EmailTemplateRenderer>();
builder.Services.AddHttpContextAccessor();
if (!string.IsNullOrEmpty(builder.Configuration["RESEND_API_KEY"]))
{
    builder.Services.AddSingleton<Svyne.Api.Email.ResendEmailService>();
    builder.Services.AddSingleton<Svyne.Api.Email.IEmailService>(sp =>
        new Svyne.Api.Email.LoggingEmailService(
            sp.GetRequiredService<Svyne.Api.Email.ResendEmailService>(),
            sp.GetRequiredService<Db>(),
            sp
        ));
}
else
{
    builder.Services.AddSingleton<Svyne.Api.Email.LocalFileEmailService>();
    builder.Services.AddSingleton<Svyne.Api.Email.IEmailService>(sp =>
        new Svyne.Api.Email.LoggingEmailService(
            sp.GetRequiredService<Svyne.Api.Email.LocalFileEmailService>(),
            sp.GetRequiredService<Db>(),
            sp
        ));
}
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ReportingAccessProvider>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddSingleton<Svyne.Api.Storage.ObjectStorage>();
builder.Services.AddSingleton<Svyne.Api.Payments.StripeService>();
builder.Services.AddSingleton<Svyne.Api.Payments.StripeWebhookHandler>();
builder.Services.AddHttpClient("salestaxzip");
builder.Services.AddSingleton<Svyne.Api.Payments.SalesTaxService>();
builder.Services.AddHostedService<Svyne.Api.Payments.HoldExpiryWorker>();
builder.Services.AddHostedService<Svyne.Api.Payments.BillingWorker>();

var jwtService = new JwtTokenService(builder.Configuration);
var validation = jwtService.ValidationParameters;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = validation.Issuer,
            ValidAudience = validation.Audience,
            IssuerSigningKey = validation.Key
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (!HttpMethods.IsPost(httpContext.Request.Method)
            || !httpContext.Request.Path.StartsWithSegments("/svyne.auth.AuthService"))
        {
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("none");
        }
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter("auth:" + ip,
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

var app = builder.Build();

await app.Services.GetRequiredService<StartupSeeder>().SeedAsync(CancellationToken.None);

app.UseMiddleware<Svyne.Api.ErrorHandling.ErrorLoggingMiddleware>();
app.UseRouting();
app.UseRateLimiter();
app.UseCors(CorsPolicy);
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapGrpcService<AuthServiceImpl>();
app.MapGrpcService<TenantServiceImpl>();
app.MapGrpcService<EventServiceImpl>();
app.MapGrpcService<VenueServiceImpl>();
app.MapGrpcService<PerformerServiceImpl>();
app.MapGrpcService<SponsorServiceImpl>();
app.MapGrpcService<BookingServiceImpl>();
app.MapGrpcService<CheckInServiceImpl>();
app.MapGrpcService<TicketServiceImpl>();
app.MapGrpcService<TableBookingServiceImpl>();
app.MapGrpcService<DashboardServiceImpl>();
app.MapGrpcService<StaffServiceImpl>();
app.MapGrpcService<InvitationServiceImpl>();
app.MapGrpcService<FeedbackServiceImpl>();
app.MapGrpcService<LogServiceImpl>();
app.MapGrpcService<FinancialServiceImpl>();
app.MapGrpcService<HealthServiceImpl>();
app.MapGrpcService<EnumServiceImpl>();
app.MapGrpcService<FeeServiceImpl>();
app.MapGrpcService<PricingServiceImpl>();
app.MapGrpcService<FloorPlanServiceImpl>();
app.MapGrpcService<ReportingServiceImpl>();
app.MapGrpcService<TenantTierServiceImpl>();
app.MapGrpcService<DeveloperBillingServiceImpl>();
app.MapGet("/", () => "Svyne gRPC API");
app.MapGet("/health/live", () => Results.Ok("live"));
app.MapGet("/health/ready", async (Db db, CancellationToken ct) =>
{
    try
    {
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new Npgsql.NpgsqlCommand("SELECT 1", connection);
        await cmd.ExecuteScalarAsync(ct);
        return Results.Ok("ready");
    }
    catch (Npgsql.NpgsqlException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/webhooks/stripe", async (
    HttpRequest request,
    IConfiguration config,
    Svyne.Api.Payments.StripeWebhookHandler handler,
    Svyne.Api.ErrorHandling.ErrorLogger errorLogger,
    CancellationToken ct) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync(ct);
    var signature = request.Headers["Stripe-Signature"].ToString();
    
    var secrets = (config["STRIPE_WEBHOOK_SECRET"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    Stripe.Event? stripeEvent = null;
    if (secrets.Length == 0)
    {
        try
        {
            stripeEvent = Stripe.EventUtility.ParseEvent(payload);
        }
        catch (Stripe.StripeException)
        {
            return Results.BadRequest("Invalid signature");
        }
    }
    else
    {
        foreach (var sec in secrets)
        {
            try
            {
                stripeEvent = Stripe.EventUtility.ConstructEvent(payload, signature, sec, throwOnApiVersionMismatch: false);
                break;
            }
            catch (Stripe.StripeException)
            {
                // Continue trying other secrets
            }
        }
        if (stripeEvent == null)
        {
            return Results.BadRequest("Invalid signature");
        }
    }

    try
    {
        await handler.HandleAsync(stripeEvent, ct);
    }
    catch (Exception ex)
    {
        await errorLogger.LogErrorAsync(
            Svyne.Api.ErrorHandling.ErrorSeverity.Critical,
            "StripeWebhookFailure",
            $"Failed handling Stripe event {stripeEvent.Type} {stripeEvent.Id}",
            ex,
            new Svyne.Api.ErrorHandling.ErrorContext
            {
                RequestPath = "/webhooks/stripe",
                RequestMethod = "POST",
                StatusCode = StatusCodes.Status500InternalServerError,
                Extra = new Dictionary<string, string>
                {
                    ["stripe_event_type"] = stripeEvent.Type,
                    ["stripe_event_id"] = stripeEvent.Id
                }
            },
            ct);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    return Results.Ok();
}).AllowAnonymous();

app.MapPost("/uploads/images", async (HttpRequest request, Db db, TenantContext tenant, Svyne.Api.Storage.ObjectStorage storage, Svyne.Api.ErrorHandling.ErrorLogger errorLogger, CancellationToken ct) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("multipart/form-data required");
    }
    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("file required");
    }
    const long maxBytes = 10 * 1024 * 1024;
    if (file.Length > maxBytes)
    {
        return Results.BadRequest("file exceeds 10MB limit");
    }
    var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
    if (!allowed.Contains(file.ContentType))
    {
        return Results.BadRequest("unsupported content type");
    }
    var entityType = form["entityType"].ToString();
    var entityId = form["entityId"].ToString();
    var storageKey = $"{entityType}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
    try
    {
        await using (var blob = file.OpenReadStream())
        {
            await storage.PutAsync(storageKey, blob, file.ContentType, ct);
        }
        await using var connection = await db.OpenAsync(tenant.UsersId, tenant.TenantsId, ct);
        await using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT sp_create_image(@et, @eid, @key, @name, @size, 0, 0, 0, @uid, NULL, NULL, NULL, @ct, NULL, @t)", connection);
        cmd.Parameters.AddWithValue("et", string.IsNullOrEmpty(entityType) ? "generic" : entityType);
        cmd.Parameters.AddWithValue("eid", Guid.TryParse(entityId, out var eid) ? eid : Guid.Empty);
        cmd.Parameters.AddWithValue("key", storageKey);
        cmd.Parameters.AddWithValue("name", file.FileName);
        cmd.Parameters.AddWithValue("size", (int)file.Length);
        cmd.Parameters.AddWithValue("uid", (object?)tenant.UsersId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ct", file.ContentType);
        cmd.Parameters.AddWithValue("t", (object?)tenant.TenantsId ?? DBNull.Value);
        var imageId = await cmd.ExecuteScalarAsync(ct);
        return Results.Ok(new { imagesId = imageId?.ToString(), storageKey });
    }
    catch (Exception ex)
    {
        await errorLogger.LogErrorAsync(
            Svyne.Api.ErrorHandling.ErrorSeverity.High,
            "ImageUploadFailure",
            $"Failed uploading image {file.FileName} for {entityType}/{entityId}",
            ex,
            new Svyne.Api.ErrorHandling.ErrorContext
            {
                RequestPath = "/uploads/images",
                RequestMethod = "POST",
                StatusCode = StatusCodes.Status500InternalServerError,
                Extra = new Dictionary<string, string>
                {
                    ["entity_type"] = entityType,
                    ["entity_id"] = entityId,
                    ["storage_key"] = storageKey
                }
            },
            ct);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapGet("/images/{imagesId}", async (string imagesId, Db db, Svyne.Api.Storage.ObjectStorage storage, CancellationToken ct) =>
{
    if (!Guid.TryParse(imagesId, out var id))
    {
        return Results.BadRequest("invalid id");
    }
    string storageKey;
    string contentType;
    await using (var connection = await db.OpenBootstrapAsync(ct))
    await using (var cmd = new Npgsql.NpgsqlCommand("SELECT storage_key, content_type FROM vw_images WHERE images_id = @id", connection))
    {
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return Results.NotFound();
        }
        storageKey = reader.GetString(0);
        contentType = reader.IsDBNull(1) ? "application/octet-stream" : reader.GetString(1);
    }
    var stream = await storage.OpenReadAsync(storageKey, ct);
    return stream is null ? Results.NotFound() : Results.File(stream, contentType);
}).AllowAnonymous();

static string AdminFrontend(IConfiguration config) =>
    config["FRONTEND_ADMIN_URL"]?.TrimEnd('/') ?? "http://admin.localhost:5173";

app.MapGet("/stripe/onboard/return", (string? tenant, IConfiguration config) =>
    Results.Redirect($"{AdminFrontend(config)}/financial?stripe=return")).AllowAnonymous();

app.MapGet("/stripe/onboard/refresh", (string? tenant, IConfiguration config) =>
    Results.Redirect($"{AdminFrontend(config)}/financial?stripe=refresh")).AllowAnonymous();


var lifecycleLogger = app.Services.GetRequiredService<Svyne.Api.ErrorHandling.ErrorLogger>();
app.Lifetime.ApplicationStarted.Register(() =>
    _ = lifecycleLogger.LogInfoAsync("SystemLifecycle", "Application started"));
app.Lifetime.ApplicationStopping.Register(() =>
    lifecycleLogger.LogInfoAsync("SystemLifecycle", "Application stopping").GetAwaiter().GetResult());

app.Run();
