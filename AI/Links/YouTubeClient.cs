public static class YouTubeClient
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://www.youtube.com/")
    };

    public static async Task<string?> GetTitleAsync(
        string videoUrl,
        CancellationToken cancellationToken = default)
    {
        var request =
            $"oembed?url={Uri.EscapeDataString(videoUrl)}&format=json";

        var response = await HttpClient.GetAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OEmbedResponse>(
            cancellationToken);

        return result?.Title;
    }

    private sealed record OEmbedResponse(
        string Title,
        string Author_Name);
}