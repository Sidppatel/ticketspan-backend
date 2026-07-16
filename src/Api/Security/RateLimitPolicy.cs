using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TicketSpan.Api.Security;

public sealed record RateLimitBucket(string PartitionKey, int PermitLimit, TimeSpan Window);

public sealed class RateLimitPolicy : IDisposable
{
    private const int DeveloperRequestsPerHour = 5000;
    private const int TenantRequestsPerHour = 1000;
    private const int AuthenticatedUserRequestsPerMinute = 100;
    private const int AuthEndpointRequestsPerMinute = 20;
    private const int AnonymousRequestsPerMinute = 100;
    private const int SlidingWindowSegmentsPerWindow = 6;

    private const string AuthServicePathPrefix = "/ticketspan.auth.AuthService";
    private const string HealthPathPrefix = "/health";
    private const string StripeWebhookPath = "/webhooks/stripe";
    private const string ImagesPathPrefix = "/images";

    private const string CloudflareConnectingIpHeader = "CF-Connecting-IP";
    private const string ForwardedForHeader = "X-Forwarded-For";
    private const string RealIpHeader = "X-Real-IP";
    private const string UnknownClientIp = "unknown";

    private const string SubjectClaim = "sub";
    private const string RoleClaim = "role";
    private const string TenantsIdClaim = "tenants_id";

    public const string LimitHeaderName = "X-RateLimit-Limit";
    public const string RemainingHeaderName = "X-RateLimit-Remaining";
    public const string ResetHeaderName = "X-RateLimit-Reset";

    private readonly PartitionedRateLimiter<HttpContext> identityLimiter;
    private readonly PartitionedRateLimiter<HttpContext> tenantLimiter;
    private readonly PartitionedRateLimiter<HttpContext> chainedLimiter;

    public RateLimitPolicy()
    {
        identityLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
            httpContext => CreatePartition(ResolveIdentityBucket(httpContext)));
        tenantLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
            httpContext => CreatePartition(ResolveTenantBucket(httpContext)));
        chainedLimiter = PartitionedRateLimiter.CreateChained(identityLimiter, tenantLimiter);
    }

    public PartitionedRateLimiter<HttpContext> Limiter => chainedLimiter;

    public void Configure(RateLimiterOptions options)
    {
        options.GlobalLimiter = chainedLimiter;
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, cancellationToken) =>
        {
            RateLimitRejection.Write(context.HttpContext, context.Lease);
            return ValueTask.CompletedTask;
        };
    }

    public RateLimitBucket? ResolveEffectiveBucket(HttpContext httpContext)
    {
        var identityBucket = ResolveIdentityBucket(httpContext);
        if (identityBucket is null)
        {
            return null;
        }
        var tenantBucket = ResolveTenantBucket(httpContext);
        if (tenantBucket is null)
        {
            return identityBucket;
        }
        var identityRemaining = ReadAvailablePermits(identityLimiter, httpContext);
        var tenantRemaining = ReadAvailablePermits(tenantLimiter, httpContext);
        return tenantRemaining < identityRemaining ? tenantBucket : identityBucket;
    }

    public long ReadRemainingPermits(HttpContext httpContext)
    {
        var identityRemaining = ReadAvailablePermits(identityLimiter, httpContext);
        var tenantRemaining = ReadAvailablePermits(tenantLimiter, httpContext);
        return Math.Min(identityRemaining, tenantRemaining);
    }

    private static long ReadAvailablePermits(
        PartitionedRateLimiter<HttpContext> limiter, HttpContext httpContext) =>
        limiter.GetStatistics(httpContext)?.CurrentAvailablePermits ?? long.MaxValue;

    private static RateLimitPartition<string> CreatePartition(RateLimitBucket? bucket)
    {
        if (bucket is null)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }
        return RateLimitPartition.GetSlidingWindowLimiter(bucket.PartitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = bucket.PermitLimit,
                Window = bucket.Window,
                SegmentsPerWindow = SlidingWindowSegmentsPerWindow,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    }

    private static RateLimitBucket? ResolveIdentityBucket(HttpContext httpContext)
    {
        if (IsExempt(httpContext))
        {
            return null;
        }
        if (httpContext.Request.Path.StartsWithSegments(AuthServicePathPrefix))
        {
            return new RateLimitBucket(
                "auth:" + ResolveClientIp(httpContext),
                AuthEndpointRequestsPerMinute,
                TimeSpan.FromMinutes(1));
        }
        var subject = httpContext.User.FindFirstValue(SubjectClaim);
        if (string.IsNullOrEmpty(subject))
        {
            return new RateLimitBucket(
                "ip:" + ResolveClientIp(httpContext),
                AnonymousRequestsPerMinute,
                TimeSpan.FromMinutes(1));
        }
        if (IsDeveloper(httpContext))
        {
            return new RateLimitBucket("dev:" + subject, DeveloperRequestsPerHour, TimeSpan.FromHours(1));
        }
        return new RateLimitBucket("user:" + subject, AuthenticatedUserRequestsPerMinute, TimeSpan.FromMinutes(1));
    }

    private static RateLimitBucket? ResolveTenantBucket(HttpContext httpContext)
    {
        if (IsExempt(httpContext)
            || httpContext.Request.Path.StartsWithSegments(AuthServicePathPrefix)
            || IsDeveloper(httpContext))
        {
            return null;
        }
        var tenantsId = httpContext.User.FindFirstValue(TenantsIdClaim);
        if (string.IsNullOrEmpty(tenantsId))
        {
            return null;
        }
        return new RateLimitBucket("tenant:" + tenantsId, TenantRequestsPerHour, TimeSpan.FromHours(1));
    }

    private static bool IsDeveloper(HttpContext httpContext) =>
        int.TryParse(httpContext.User.FindFirstValue(RoleClaim), out var role)
        && role == Lookups.UserRoles.Developer;

    private static bool IsExempt(HttpContext httpContext)
    {
        var path = httpContext.Request.Path;
        return path.StartsWithSegments(HealthPathPrefix)
            || path.StartsWithSegments(ImagesPathPrefix)
            || path.StartsWithSegments(StripeWebhookPath);
    }

    private static string ResolveClientIp(HttpContext httpContext)
    {
        var cloudflareIp = httpContext.Request.Headers[CloudflareConnectingIpHeader].ToString();
        if (!string.IsNullOrWhiteSpace(cloudflareIp))
        {
            return cloudflareIp.Trim();
        }
        var forwardedFor = httpContext.Request.Headers[ForwardedForHeader].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var leftMost = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (leftMost.Length > 0)
            {
                return leftMost[0];
            }
        }
        var realIp = httpContext.Request.Headers[RealIpHeader].ToString();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp.Trim();
        }
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClientIp;
    }

    public void Dispose()
    {
        chainedLimiter.Dispose();
        identityLimiter.Dispose();
        tenantLimiter.Dispose();
    }
}
