using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Svyne.Api.Security;

// Event managers (role 4) are scoped admins: full event-editing on assigned events,
// nothing tenant-wide (financials, settings, invitations, staff management, other
// events' bookings). Enforcement is fail-closed by a service whitelist — any gRPC
// service not listed here is denied for role 4, so a service added later stays
// locked until someone deliberately opens it. Per-event scoping (which events they
// may touch inside the allowed services) is enforced separately by RLS and the
// app.can_access_event() guards in the service methods.
public sealed class EventManagerAuthorizationInterceptor : Interceptor
{
    private const int EventManagerRole = 4;

    private static readonly HashSet<string> AllowedServices = new(StringComparer.Ordinal)
    {
        "AuthService",
        "EventService",
        "TicketService",
        "TableBookingService",
        "CheckInService",
        "FloorPlanService",
        "PricingService",
        "FeeService",
        "EnumService",
        "VenueService",
        "PerformerService",
        "SponsorService",
        "HealthService",
    };

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var tenantContext = context.GetHttpContext().RequestServices.GetRequiredService<TenantContext>();
        if (tenantContext.Role == EventManagerRole && !IsAllowed(context.Method))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Not available for event managers"));
        }
        return await continuation(request, context);
    }

    // context.Method looks like "/svyne.event.EventService/GetEvent".
    private static bool IsAllowed(string method)
    {
        var segments = method.Split('/');
        if (segments.Length < 2)
        {
            return false;
        }
        var fullService = segments[1];
        var simpleName = fullService[(fullService.LastIndexOf('.') + 1)..];
        return AllowedServices.Contains(simpleName);
    }
}
