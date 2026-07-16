using System.Globalization;
using System.Threading.RateLimiting;

namespace TicketSpan.Api.Security;

public static class RateLimitRejection
{
    private const int GrpcStatusResourceExhausted = 8;
    private const string GrpcWebContentTypePrefix = "application/grpc-web";
    private const string GrpcStatusHeaderName = "grpc-status";
    private const string GrpcMessageHeaderName = "grpc-message";
    private const string RetryAfterHeaderName = "Retry-After";
    private const string RejectionMessage = "Rate limit exceeded";

    public static void Write(HttpContext httpContext, RateLimitLease lease)
    {
        var retryAfterSeconds = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : 0;
        if (retryAfterSeconds > 0)
        {
            httpContext.Response.Headers[RetryAfterHeaderName] =
                retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        }
        if (!IsGrpcWebRequest(httpContext))
        {
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = httpContext.Request.ContentType ?? GrpcWebContentTypePrefix + "+proto";
        httpContext.Response.Headers[GrpcStatusHeaderName] =
            GrpcStatusResourceExhausted.ToString(CultureInfo.InvariantCulture);
        httpContext.Response.Headers[GrpcMessageHeaderName] = RejectionMessage;
    }

    public static bool IsGrpcWebRequest(HttpContext httpContext) =>
        httpContext.Request.ContentType?.StartsWith(GrpcWebContentTypePrefix, StringComparison.OrdinalIgnoreCase) == true;
}
