using System.Security.Claims;
using Grpc.Core;

namespace TicketSpan.Api.Endpoints.Common;

public sealed class UnaryServerCallContext : ServerCallContext
{
    private readonly CancellationToken cancellationToken;
    private readonly Metadata requestHeaders;
    private readonly Metadata responseTrailers;
    private Status status;
    private WriteOptions? writeOptions;

    public UnaryServerCallContext(CancellationToken cancellationToken, Metadata? requestHeaders = null)
    {
        this.cancellationToken = cancellationToken;
        this.requestHeaders = requestHeaders ?? new Metadata();
        this.responseTrailers = new Metadata();
        this.status = Status.DefaultSuccess;
    }

    protected override string MethodCore => "/TicketSpan.Api/Unary";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "127.0.0.1";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => requestHeaders;
    protected override CancellationToken CancellationTokenCore => cancellationToken;
    protected override Metadata ResponseTrailersCore => responseTrailers;
    protected override Status StatusCore { get => status; set => status = value; }
    protected override WriteOptions? WriteOptionsCore { get => writeOptions; set => writeOptions = value; }
    protected override AuthContext AuthContextCore => new AuthContext(null, new Dictionary<string, List<AuthProperty>>());

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
        Task.CompletedTask;
}
