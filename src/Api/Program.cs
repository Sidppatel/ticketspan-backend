using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Svyne.Api.Data;
using Svyne.Api.Middleware;
using Svyne.Api.Security;
using Svyne.Api.Services;

System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

var http2Only = builder.Configuration["GRPC_HTTP2_ONLY"] == "true";
builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(listen =>
        listen.Protocols = http2Only ? HttpProtocols.Http2 : HttpProtocols.Http1AndHttp2));

builder.Services.AddGrpc();
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddSingleton<Svyne.Api.Storage.ObjectStorage>();

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

var app = builder.Build();

app.UseRouting();
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
app.MapGrpcService<PurchaseServiceImpl>();
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

app.MapPost("/webhooks/stripe", async (HttpRequest request, IConfiguration config) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync();
    var signature = request.Headers["Stripe-Signature"].ToString();
    var secret = config["STRIPE_WEBHOOK_SECRET"];
    if (!string.IsNullOrEmpty(secret))
    {
        try
        {
            Stripe.EventUtility.ValidateSignature(payload, signature, secret);
        }
        catch (Stripe.StripeException)
        {
            return Results.BadRequest("Invalid signature");
        }
    }
    return Results.Ok();
}).AllowAnonymous();

app.MapPost("/uploads/images", async (HttpRequest request, Db db, TenantContext tenant, Svyne.Api.Storage.ObjectStorage storage, CancellationToken ct) =>
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
}).RequireAuthorization();

app.Run();
