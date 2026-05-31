using System.Net;
using HtmlAgilityPack;

public static class WebPageMetadata
{
    private static readonly HttpClient HttpClient = new();

    public sealed record Metadata(
        string? Title,
        string? Description,
        string? Type);

    public static async Task<Metadata> GetMetadataAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var html = await HttpClient.GetStringAsync(url, cancellationToken);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var ogTitle = doc.DocumentNode
            .SelectSingleNode("//meta[@property='og:title']")
            ?.GetAttributeValue("content", string.Empty)
            ?.Trim();

        var pageTitle = doc.DocumentNode
            .SelectSingleNode("//title")
            ?.InnerText
            ?.Trim();

        var ogDescription = doc.DocumentNode
            .SelectSingleNode("//meta[@property='og:description']")
            ?.GetAttributeValue("content", string.Empty)
            ?.Trim();

        var metaDescription = doc.DocumentNode
            .SelectSingleNode("//meta[@name='description']")
            ?.GetAttributeValue("content", string.Empty)
            ?.Trim();

        var ogType = doc.DocumentNode
            .SelectSingleNode("//meta[@property='og:type']")
            ?.GetAttributeValue("content", string.Empty)
            ?.Trim();

        var title = FirstNonEmpty(ogTitle, pageTitle);
        var description = FirstNonEmpty(ogDescription, metaDescription);
        var type = FirstNonEmpty(ogType, "website");

        return new Metadata(
            HtmlDecode(title),
            HtmlDecode(description),
            HtmlDecode(type));
    }

    public static async Task<string?> GetTitleAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(url, cancellationToken);
        return metadata.Title;
    }

    private static string? HtmlDecode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : WebUtility.HtmlDecode(value);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}