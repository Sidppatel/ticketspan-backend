using Grpc.Core;
using Grpc.Core.Interceptors;
using TicketSpan.Api.Security;

namespace TicketSpan.Api.ErrorHandling;

public sealed class ErrorLoggingInterceptor : Interceptor
{
    private readonly ErrorLogger errorLogger;

    public ErrorLoggingInterceptor(ErrorLogger errorLogger)
    {
        this.errorLogger = errorLogger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var httpContext = context.GetHttpContext();
            var tenantContext = httpContext.RequestServices.GetRequiredService<TenantContext>();
            var errorId = await errorLogger.LogErrorAsync(
                ErrorSeverity.High,
                "UnhandledGrpcException",
                $"Unhandled exception in {context.Method}",
                exception,
                ErrorContext.FromHttpContext(httpContext, tenantContext),
                CancellationToken.None);
            throw new RpcException(new Status(StatusCode.Internal,
                errorId is { } id ? $"Internal error (ref {id})" : "Internal error"));
        }
    }
}
