using TicketSpan.Api.Security;

namespace TicketSpan.Api.ErrorHandling;

public sealed class ErrorLoggingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ErrorLogger errorLogger;

    public ErrorLoggingMiddleware(RequestDelegate next, ErrorLogger errorLogger)
    {
        this.next = next;
        this.errorLogger = errorLogger;
    }

    public async Task InvokeAsync(HttpContext httpContext, TenantContext tenantContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var context = ErrorContext.FromHttpContext(httpContext, tenantContext) with
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            var errorId = await errorLogger.LogErrorAsync(
                ErrorSeverity.High,
                "UnhandledHttpException",
                $"Unhandled exception on {httpContext.Request.Method} {httpContext.Request.Path}",
                exception,
                context,
                CancellationToken.None);
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsync(
                    errorId is { } id ? $"Internal error (ref {id})" : "Internal error");
            }
        }
    }
}
