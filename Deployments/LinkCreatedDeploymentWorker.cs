public sealed class LinkCreatedDeploymentWorker : BackgroundService
{
    private readonly ILinkCreatedDeploymentQueue queue;
    private readonly DeployWebhookDispatcher dispatcher;
    private readonly ILogger<LinkCreatedDeploymentWorker> logger;

    public LinkCreatedDeploymentWorker(
        ILinkCreatedDeploymentQueue queue,
        DeployWebhookDispatcher dispatcher,
        ILogger<LinkCreatedDeploymentWorker> logger)
    {
        this.queue = queue;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await dispatcher.DispatchAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to trigger deploy webhook for inserted link {LinkId}.",
                    request.LinkId);
            }
        }
    }
}
