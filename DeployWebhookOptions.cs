public sealed class DeployWebhookOptions
{
    public bool Enabled { get; set; } = true;

    public string Url { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string EventType { get; set; } = "link-added";
}
