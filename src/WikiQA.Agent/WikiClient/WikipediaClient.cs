using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using WikiQA.Agent.Models;

namespace WikiQA.Agent.WikiClient;

public class WikipediaClient(ILogger<WikipediaClient> logger)
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "WikiQA-Agent/1.0 (https://github.com/raghu/WikiQA-Agent)" }
        }
    };
    private const int MaxRetries = 3;

    public async Task<WikipediaResult?> SearchAsync(string query, string correlationId)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("[{CorrelationId}] Wikipedia search attempt {Attempt} for: {Query}", correlationId, attempt, query);

                var searchUrl = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&format=json&utf8=1&srlimit=1";
                var searchResult = await Http.GetFromJsonAsync<JsonNode>(searchUrl);

                var hit = searchResult?["query"]?["search"]?[0];
                if (hit is null)
                {
                    logger.LogWarning("[{CorrelationId}] No search results found for: {Query}", correlationId, query);
                    return null;
                }

                var title   = hit["title"]?.GetValue<string>() ?? string.Empty;
                var snippet = hit["snippet"]?.GetValue<string>() ?? string.Empty;
                var pageUrl = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";

                logger.LogInformation("[{CorrelationId}] Found article: {Title} | {PageUrl}", correlationId, title, pageUrl);

                return new WikipediaResult(title, snippet, pageUrl);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("[{CorrelationId}] Rate limited on attempt {Attempt}, retrying in {Delay}s", correlationId, attempt, attempt);
                if (attempt == MaxRetries) return null;
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{CorrelationId}] Unexpected error searching Wikipedia for: {Query}", correlationId, query);
                return null;
            }
        }
        return null;
    }
}
