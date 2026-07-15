using Grpc.Core;
using Grpc.Core.Interceptors;

namespace TicketSpan.Api.Security;

public sealed class EventManagerAuthorizationInterceptor : Interceptor
{
    private const int EventManagerRole = Lookups.UserRoles.EventManager;

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
