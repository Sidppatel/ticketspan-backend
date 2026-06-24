using System.Security.Claims;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;

namespace Svyne.Api.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, TenantContext tenantContext, Db db)
    {
        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(sub, out var usersId))
            {
                tenantContext.UsersId = usersId;
            }
            var roleClaim = user.FindFirstValue("role") ?? user.FindFirstValue(ClaimTypes.Role);
            tenantContext.Role = int.TryParse(roleClaim, out var role) ? role : 0;
            tenantContext.TenantSlug = user.FindFirstValue("tenant_slug") ?? string.Empty;
            if (Guid.TryParse(user.FindFirstValue("tenants_id"), out var tenantsId))
            {
                tenantContext.TenantsId = tenantsId;
            }
            if (tenantContext.TenantsId is null && tenantContext.Role != 99)
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await httpContext.Response.WriteAsync("Tenant context required");
                return;
            }
        }
        else
        {
            var slug = httpContext.Request.Headers["x-tenant-slug"].ToString();
            if (!string.IsNullOrEmpty(slug))
            {
                var tenantsId = await ResolveTenantAsync(db, slug, httpContext.RequestAborted);
                if (tenantsId is { } id)
                {
                    tenantContext.TenantsId = id;
                    tenantContext.TenantSlug = slug;
                    tenantContext.Role = 0;
                }
            }
        }
        await next(httpContext);
    }

    private static async Task<Guid?> ResolveTenantAsync(Db db, string slug, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT tenants_id FROM tenants WHERE slug = @s AND archived_at IS NULL", connection);
        cmd.Parameters.AddWithValue("s", slug);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }
}
