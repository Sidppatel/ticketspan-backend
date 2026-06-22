using System.Security.Claims;
using Svyne.Api.Security;

namespace Svyne.Api.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, TenantContext tenantContext)
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
        await next(httpContext);
    }
}
