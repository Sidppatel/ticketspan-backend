using Npgsql;
using OpenIddict.Abstractions;

namespace TicketSpan.Api.Data;

public static class OpenIddictSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var db = scope.ServiceProvider.GetRequiredService<Db>();

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

        var redirectUris = new HashSet<Uri>
        {
            new("http://localhost:5173/callback"),
            new("http://localhost:5173/silent-renew.html"),
            new("https://ticketspan.com/callback"),
            new("https://ticketspan.com/silent-renew.html")
        };

        var postLogoutUris = new HashSet<Uri>
        {
            new("http://localhost:5173/"),
            new("https://ticketspan.com/")
        };

        foreach (var slug in tenantSlugs)
        {
            redirectUris.Add(new Uri($"http://{slug}.localhost:5173/callback"));
            redirectUris.Add(new Uri($"http://{slug}.localhost:5173/silent-renew.html"));
            redirectUris.Add(new Uri($"https://{slug}.ticketspan.com/callback"));
            redirectUris.Add(new Uri($"https://{slug}.ticketspan.com/silent-renew.html"));

            postLogoutUris.Add(new Uri($"http://{slug}.localhost:5173/"));
            postLogoutUris.Add(new Uri($"https://{slug}.ticketspan.com/"));
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
}
