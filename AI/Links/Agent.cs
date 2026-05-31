using System;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;

public class Agent
{
    public const string Prompt = @"Classify a link for a personal archive.

Input is only a URL string.

Categories:
video, blog, product, repo, docs, link

Definitions:
video = watchable video or video platform page like youtube
blog = article, essay, tutorial, news, blog post
product = tool, app, SaaS, service, company product page
repo = source code repository
docs = official docs, API reference, manual, guide
link = fallback when unclear

For YouTube URLs:
- use GetYoutubeTitle tool
- set contentType to video

For non-YouTube URLs:
- call GetWebsiteMetadata tool first to fetch title, description, and pageType
- use description and pageType as primary signals
- use url as a secondary signal

Choose the best category. If uncertain, choose link.

For title in output:
- prefer metadata title from tools when present
- if still missing, use GetWebsiteTitle

If title is not found, return null.
";
    public static AIAgent CreateAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AI:Ollama:Endpoint"]
            ?? "https://ollama.home.arpa";
        var modelId = configuration["AI:Ollama:ModelId"]
            ?? "qwen3.5:2b";

        var chatClient = new OllamaChatClient(
            new Uri(endpoint),
            modelId: modelId);

        AIAgent agent = chatClient.AsAIAgent(
            instructions: Prompt,
            tools:
            [
                AIFunctionFactory.Create(GetYoutubeTitle),
                AIFunctionFactory.Create(GetWebsiteTitle),
                AIFunctionFactory.Create(GetWebsiteMetadata)
            ]);
        return agent;
    }


    [Description("Get the youtube video title")]
    public static async Task<string?> GetYoutubeTitle([Description("The youtube url")] string url)
    {
        var title = await YouTubeClient.GetTitleAsync(url);

        return title;
    }

    [Description("Get the title of the website")]
    public static async Task<string?> GetWebsiteTitle([Description("The website url")] string url)
    {
        var title = await WebPageMetadata.GetTitleAsync(url);

        return title;
    }

    [Description("Get website metadata as JSON with title, description, and pageType")]
    public static async Task<string> GetWebsiteMetadata([Description("The website url")] string url)
    {
        var metadata = await WebPageMetadata.GetMetadataAsync(url);

        return JsonSerializer.Serialize(new
        {
            title = metadata.Title,
            description = metadata.Description,
            pageType = metadata.Type
        });
    }
}
