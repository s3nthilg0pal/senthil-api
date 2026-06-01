using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

public sealed class DeployWebhookDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly DeployWebhookOptions options;
    private readonly ILogger<DeployWebhookDispatcher> logger;

    public DeployWebhookDispatcher(
        HttpClient httpClient,
        IOptions<DeployWebhookOptions> options,
        ILogger<DeployWebhookDispatcher> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<bool> DispatchAsync(LinkCreatedDeploymentRequest request, CancellationToken ct)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Deploy webhook is disabled.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            logger.LogInformation("Deploy webhook URL is not configured.");
            return false;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, options.Url);

        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }

        var body = new
        {
            event_type = options.EventType,
            client_payload = new
            {
                link = new
                {
                    id = request.LinkId,
                    title = request.Title,
                    url = request.Url,
                    content_type = request.ContentType,
                    date = request.Date
                }
            }
        };

        message.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(message, ct);

        if (!response.IsSuccessStatusCode)
        {
            var responseText = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "Deploy webhook failed with status {StatusCode}: {Response}",
                response.StatusCode,
                responseText);

            return false;
        }

        logger.LogInformation(
            "Triggered deploy webhook for inserted link {LinkId}.",
            request.LinkId);

        return true;
    }
}
