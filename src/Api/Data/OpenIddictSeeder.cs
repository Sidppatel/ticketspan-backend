using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenIddict.Abstractions;

namespace TicketSpan.Api.Data;

public static class OpenIddictSeeder
{
    private static readonly string[] SystemSubdomains = ["admin", "staff", "developer"];

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        var isDev = environment.IsDevelopment() || string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
        var frontendPort = configuration["FRONTEND_PORT"] ?? "5173";
        var localPortSuffix = string.IsNullOrEmpty(frontendPort) || frontendPort == "80" ? "" : $":{frontendPort}";
        var mainDomain = configuration["MAIN_DOMAIN"] ?? configuration["FRONTEND_BASE_DOMAIN"] ?? configuration["CORS_BASE_DOMAIN"];
        var hasMainDomain = !string.IsNullOrWhiteSpace(mainDomain) && !mainDomain.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        var tenantSlugs = new List<string>();
        await using (var connection = await db.OpenAsync(null, null, CancellationToken.None))
        await using (var cmd = new NpgsqlCommand("SELECT slug FROM vw_tenants WHERE archived_at IS NULL", connection))
        await using (var reader = await cmd.ExecuteReaderAsync(CancellationToken.None))
        {
            while (await reader.ReadAsync(CancellationToken.None))
            {
                if (!reader.IsDBNull(0))
                {
                    var s = reader.GetString(0).Trim();
                    if (!string.IsNullOrEmpty(s))
                    {
                        tenantSlugs.Add(s);
                    }
                }
            }
        }

        var redirectUris = new HashSet<Uri>();
        var postLogoutUris = new HashSet<Uri>();

        void AddHostUris(string host, string scheme)
        {
            redirectUris.Add(new Uri($"{scheme}://{host}/callback"));
            redirectUris.Add(new Uri($"{scheme}://{host}/silent-renew.html"));
            postLogoutUris.Add(new Uri($"{scheme}://{host}/"));
        }

        if (isDev)
        {
            AddHostUris($"localhost{localPortSuffix}", "http");
            foreach (var sub in SystemSubdomains)
            {
                AddHostUris($"{sub}.localhost{localPortSuffix}", "http");
            }
            foreach (var slug in tenantSlugs)
            {
                AddHostUris($"{slug}.localhost{localPortSuffix}", "http");
            }
        }

        if (hasMainDomain)
        {
            AddHostUris(mainDomain!, "https");
            foreach (var sub in SystemSubdomains)
            {
                AddHostUris($"{sub}.{mainDomain}", "https");
            }
            foreach (var slug in tenantSlugs)
            {
                AddHostUris($"{slug}.{mainDomain}", "https");
            }
        }

        var client = await manager.FindByClientIdAsync("ticketspan_spa");
        if (client is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "ticketspan_spa",
                DisplayName = "TicketSpan SPA Client",
                ClientType = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.Endpoints.EndSession,
                    OpenIddictConstants.Permissions.Endpoints.Revocation,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.GrantTypes.Password,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "ticketspan_api"
                }
            };

            foreach (var rUri in redirectUris)
            {
                descriptor.RedirectUris.Add(rUri);
            }
            foreach (var pUri in postLogoutUris)
            {
                descriptor.PostLogoutRedirectUris.Add(pUri);
            }

            await manager.CreateAsync(descriptor);
        }
        else
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, client);

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Password);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Email);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Profile);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Roles);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "ticketspan_api");

            foreach (var rUri in redirectUris)
            {
                if (!descriptor.RedirectUris.Contains(rUri))
                {
                    descriptor.RedirectUris.Add(rUri);
                }
            }
            foreach (var pUri in postLogoutUris)
            {
                if (!descriptor.PostLogoutRedirectUris.Contains(pUri))
                {
                    descriptor.PostLogoutRedirectUris.Add(pUri);
                }
            }

            await manager.UpdateAsync(client, descriptor);
        }
    }

    public static async Task RegisterTenantUrisAsync(
        IOpenIddictApplicationManager manager,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        var client = await manager.FindByClientIdAsync("ticketspan_spa");
        if (client is null)
        {
            return;
        }

        var isDev = environment.IsDevelopment() || string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
        var frontendPort = configuration["FRONTEND_PORT"] ?? "5173";
        var localPortSuffix = string.IsNullOrEmpty(frontendPort) || frontendPort == "80" ? "" : $":{frontendPort}";
        var mainDomain = configuration["MAIN_DOMAIN"] ?? configuration["FRONTEND_BASE_DOMAIN"] ?? configuration["CORS_BASE_DOMAIN"];
        var hasMainDomain = !string.IsNullOrWhiteSpace(mainDomain) && !mainDomain.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        var newRedirectUris = new List<Uri>();
        var newPostLogoutUris = new List<Uri>();

        if (isDev)
        {
            newRedirectUris.Add(new Uri($"http://{slug}.localhost{localPortSuffix}/callback"));
            newRedirectUris.Add(new Uri($"http://{slug}.localhost{localPortSuffix}/silent-renew.html"));
            newPostLogoutUris.Add(new Uri($"http://{slug}.localhost{localPortSuffix}/"));
        }

        if (hasMainDomain)
        {
            newRedirectUris.Add(new Uri($"https://{slug}.{mainDomain}/callback"));
            newRedirectUris.Add(new Uri($"https://{slug}.{mainDomain}/silent-renew.html"));
            newPostLogoutUris.Add(new Uri($"https://{slug}.{mainDomain}/"));
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, client);

        var changed = false;
        foreach (var uri in newRedirectUris)
        {
            if (!descriptor.RedirectUris.Contains(uri))
            {
                descriptor.RedirectUris.Add(uri);
                changed = true;
            }
        }
        foreach (var uri in newPostLogoutUris)
        {
            if (!descriptor.PostLogoutRedirectUris.Contains(uri))
            {
                descriptor.PostLogoutRedirectUris.Add(uri);
                changed = true;
            }
        }

        if (changed)
        {
            await manager.UpdateAsync(client, descriptor);
        }
    }
}
