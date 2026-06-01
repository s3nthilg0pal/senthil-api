using System.Threading.Channels;

public sealed record LinkCreatedDeploymentRequest(
    int LinkId,
    string Title,
    string Url,
    string ContentType,
    DateTime Date);

public interface ILinkCreatedDeploymentQueue
{
    ValueTask EnqueueAsync(LinkCreatedDeploymentRequest request, CancellationToken ct);

    IAsyncEnumerable<LinkCreatedDeploymentRequest> ReadAllAsync(CancellationToken ct);
}

public sealed class LinkCreatedDeploymentQueue : ILinkCreatedDeploymentQueue
{
    private readonly Channel<LinkCreatedDeploymentRequest> channel =
        Channel.CreateUnbounded<LinkCreatedDeploymentRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(LinkCreatedDeploymentRequest request, CancellationToken ct)
    {
        return channel.Writer.WriteAsync(request, ct);
    }

    public IAsyncEnumerable<LinkCreatedDeploymentRequest> ReadAllAsync(CancellationToken ct)
    {
        return channel.Reader.ReadAllAsync(ct);
    }
}
