using System.Globalization;
using TicketSpan.Api.Security;

namespace TicketSpan.Api.Middleware;

public sealed class RateLimitHeaderMiddleware
{
    private readonly RequestDelegate next;

    public RateLimitHeaderMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public Task InvokeAsync(HttpContext httpContext, RateLimitPolicy rateLimitPolicy)
    {
        httpContext.Response.OnStarting(() =>
        {
            var bucket = rateLimitPolicy.ResolveEffectiveBucket(httpContext);
            if (bucket is not null)
            {
                var remaining = rateLimitPolicy.ReadRemainingPermits(httpContext);
                var resetAt = DateTimeOffset.UtcNow.Add(bucket.Window).ToUnixTimeSeconds();
                httpContext.Response.Headers[RateLimitPolicy.LimitHeaderName] =
                    bucket.PermitLimit.ToString(CultureInfo.InvariantCulture);
                httpContext.Response.Headers[RateLimitPolicy.RemainingHeaderName] =
                    Math.Max(remaining, 0).ToString(CultureInfo.InvariantCulture);
                httpContext.Response.Headers[RateLimitPolicy.ResetHeaderName] =
                    resetAt.ToString(CultureInfo.InvariantCulture);
            }
            return Task.CompletedTask;
        });
        return next(httpContext);
    }
}
