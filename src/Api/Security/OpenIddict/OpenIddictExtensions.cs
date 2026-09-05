using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using TicketSpan.Api.Data;

namespace TicketSpan.Api.Security.OpenIddict;

public static class OpenIddictExtensions
{
    public static IServiceCollection AddTicketSpanOpenIddict(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var dbHost = configuration["DB_HOST"] ?? "127.0.0.1";
        var dbPort = configuration["DB_PORT"] ?? "5432";
        var dbUser = configuration["DB_USER"] ?? "ep_dev";
        var dbName = configuration["DB_NAME"] ?? "event_platform";
        var dbPassword = configuration["DB_PASSWORD"] ?? "ep_dev_password";
        var connStr = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

        services.AddDbContext<OpenIddictDbContext>(options =>
        {
            options.UseNpgsql(connStr);
            options.UseSnakeCaseNamingConvention();
            options.UseOpenIddict();
        });

        var cookieDomain = configuration["SSO_COOKIE_DOMAIN"];

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = "ts_sso";
            options.Cookie.HttpOnly = true;
            if (!string.IsNullOrWhiteSpace(cookieDomain))
            {
                options.Cookie.Domain = cookieDomain;
            }
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
        });

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<OpenIddictDbContext>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                       .SetTokenEndpointUris("/connect/token")
                       .SetEndSessionEndpointUris("/connect/logout")
                       .SetUserInfoEndpointUris("/connect/userinfo")
                       .SetRevocationEndpointUris("/connect/revocation");

                options.AllowAuthorizationCodeFlow()
                       .AllowRefreshTokenFlow()
                       .AllowPasswordFlow();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Roles,
                    OpenIddictConstants.Scopes.OfflineAccess,
                    "ticketspan_api");

                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                var aspNetCoreBuilder = options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableEndSessionEndpointPassthrough()
                       .EnableUserInfoEndpointPassthrough();

                if (environment.IsDevelopment())
                {
                    aspNetCoreBuilder.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }
}
