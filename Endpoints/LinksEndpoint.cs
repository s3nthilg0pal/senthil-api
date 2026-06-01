using System.Text.Json;
using System.Diagnostics;
using System.Text.Json.Serialization;
using FastEndpoints;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

public sealed class LinksEndpoint(AppDbContext db) : EndpointWithoutRequest<LinksResponse>
{
    private static string NormalizeContentType(string contentType)
    {
        return contentType.Trim().ToLowerInvariant();
    }

    public override void Configure()
    {
        Get("/links");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var links = await db.Links
            .AsNoTracking()
            .OrderByDescending(link => link.Date)
            .ThenByDescending(link => link.Id)
            .Select(link => new LinkResponse(
                link.Title,
                link.Url,
                NormalizeContentType(link.ContentType),
                DateOnly.FromDateTime(link.Date)))
            .ToListAsync(ct);

        await Send.OkAsync(new LinksResponse(links), ct);
    }
}

public sealed record LinksResponse(
    IReadOnlyList<LinkResponse> Links);

public sealed record LinkResponse(
    string Title,
    string Url,
    [property: JsonPropertyName("content_type")]
    string ContentType,
    DateOnly Date);

public sealed class UpsertLinkEndpoint(
    AppDbContext db,
    AIAgent agent,
    ILogger<UpsertLinkEndpoint> logger,
    ILinkCreatedDeploymentQueue deploymentQueue) : Endpoint<LinkRequest, LinkResponse>
{
    private static string NormalizeContentType(string contentType)
    {
        return contentType.Trim().ToLowerInvariant();
    }

    private sealed record LlmLinkInput(
        string Url,
        string? Title,
        string? Description,
        string? PageType);

    private static bool IsYouTubeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();

        return host == "youtu.be"
            || host.EndsWith("youtube.com");
    }

    private static async Task<LlmLinkInput> GetMetadataForLlmAsync(
        string url,
        CancellationToken ct)
    {
        if (IsYouTubeUrl(url))
        {
            try
            {
                var title = await YouTubeClient.GetTitleAsync(url, ct);
                return new LlmLinkInput(url, title, null, "video");
            }
            catch
            {
                return new LlmLinkInput(url, null, null, "video");
            }
        }

        try
        {
            var metadata = await WebPageMetadata.GetMetadataAsync(url, ct);
            return new LlmLinkInput(url, metadata.Title, metadata.Description, metadata.Type);
        }
        catch
        {
            return new LlmLinkInput(url, null, null, null);
        }
    }

    private static (string? Title, string ContentType) GetFallbackMetadata(LlmLinkInput metadata)
    {
        var isVideo = IsYouTubeUrl(metadata.Url)
            || string.Equals(metadata.PageType, "video", StringComparison.OrdinalIgnoreCase);

        return isVideo
            ? (metadata.Title, "video")
            : (metadata.Title, "link");
    }

    public override void Configure()
    {
        Post("/links");
        AuthSchemes(ApiKeyAuthenticationHandler.SchemeName);
    }

    public override async Task HandleAsync(LinkRequest req, CancellationToken ct)
    {
        var source = IsYouTubeUrl(req.Url) ? "youtube" : "web";
        var contentType = string.IsNullOrWhiteSpace(req.ContentType)
            ? "link"
            : req.ContentType;

        var createdAt = DateTime.UtcNow;

        var link = await db.Links
            .SingleOrDefaultAsync(link => link.Url == req.Url, ct);

        const string JsonSchema = """
{
  "type": "object",
  "properties": {
    "url": {
      "type": "string"
    },
    "contentType": {
      "type": "string",
      "enum": [
        "video",
        "blog",
        "product",
        "repo",
        "docs",
        "link"
      ]
    },
    "title": {
      "type": "string"
    }
  },
  "required": [
    "url",
    "contentType",
    "title"
  ],
  "additionalProperties": false
}
""";

        ChatClientAgentRunOptions runOptions = new()
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(JsonElement.Parse(JsonSchema), "LinkInfo", "Information about a link"),
            ChatOptions = new()
            {
                Reasoning = new()
                {
                    Effort = ReasoningEffort.Low // Overwrites the default 
                }
            }
        };

        var llmInputMetadata = await GetMetadataForLlmAsync(req.Url, ct);

        string? responseType = null;
        string? responseTitle = null;
        var llmCallFailed = false;
        var llmAttempt = Stopwatch.StartNew();

        try
        {
            var response = await agent.RunAsync(req.Url, options: runOptions);

            if (!string.IsNullOrWhiteSpace(response.Text))
            {
                using var json = JsonDocument.Parse(response.Text);

                if (json.RootElement.TryGetProperty("contentType", out var contentTypeElement))
                {
                    responseType = contentTypeElement.GetString();

                    if (!string.IsNullOrWhiteSpace(responseType))
                    {
                        AgentMetrics.RecordCategory(source, "llm", responseType);
                    }
                }

                if (json.RootElement.TryGetProperty("title", out var titleElement))
                {
                    responseTitle = titleElement.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            llmCallFailed = true;
            logger.LogError(
                ex,
                "LLM link classification failed for {Source} URL {Url}. Falling back to local metadata.",
                source,
                req.Url);
        }
        finally
        {
            llmAttempt.Stop();
        }

        var usedFallback = false;
        if (string.IsNullOrWhiteSpace(responseType) || string.IsNullOrWhiteSpace(responseTitle))
        {
            usedFallback = true;
            var fallback = GetFallbackMetadata(llmInputMetadata);

            if (string.IsNullOrWhiteSpace(responseType))
            {
                responseType = fallback.ContentType;
                AgentMetrics.RecordCategory(source, "fallback", fallback.ContentType);
            }

            if (string.IsNullOrWhiteSpace(responseTitle))
            {
                responseTitle = fallback.Title;
            }
        }

        var runOutcome = llmCallFailed
            ? "error"
            : (usedFallback ? "partial" : "success");

        AgentMetrics.RecordRun(source, runOutcome, llmAttempt.Elapsed.TotalMilliseconds);

        if (runOutcome == "success")
        {
            AgentMetrics.RecordSuccess(source);
        }

        if (usedFallback)
        {
            var fallbackReason = llmCallFailed
                ? "llm_error"
                : "missing_fields";

            AgentMetrics.RecordFallback(source, fallbackReason);
        }

        var finalType = NormalizeContentType(responseType ?? contentType);
        var finalTitle = responseTitle ?? req.Title;

        AgentMetrics.RecordResultType(source, finalType);


        var isNewLink = link is null;

        if (isNewLink)
        {
            link = new Link
            {
                Title = finalTitle,
                Url = req.Url,
                ContentType = finalType,
                Date = createdAt,
                CreatedAt = createdAt
            };

            db.Links.Add(link);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(link);

            link.Title = finalTitle;
            link.ContentType = finalType;
            link.Date = createdAt;
        }

        await db.SaveChangesAsync(ct);

        if (isNewLink)
        {
            await deploymentQueue.EnqueueAsync(new LinkCreatedDeploymentRequest(
                link.Id,
                link.Title,
                link.Url,
                link.ContentType,
                link.Date), ct);
        }

        await Send.OkAsync(new LinkResponse(
            link.Title,
            link.Url,
            link.ContentType,
            DateOnly.FromDateTime(link.Date)), ct);
    }
}

public sealed record LinkRequest(
    string Title,
    string Url,
    string? ContentType,
    DateTime Date);

public sealed record LinkInfo(string url, ContentCategory contentType, string title);

public enum ContentCategory
{
    video,
    blog,
    product,
    repo,
    docs,
    link
}
