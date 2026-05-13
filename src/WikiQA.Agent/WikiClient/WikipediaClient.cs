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

    public async Task<IReadOnlyList<WikipediaResult>> SearchAsync(string query, string correlationId)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("[{CorrelationId}] Wikipedia search attempt {Attempt} for: {Query}", correlationId, attempt, query);

                var searchUrl = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&format=json&utf8=1&srlimit=3";
                var searchResult = await Http.GetFromJsonAsync<JsonNode>(searchUrl);

                var hits = searchResult?["query"]?["search"]?.AsArray();
                if (hits is null || hits.Count == 0)
                {
                    logger.LogWarning("[{CorrelationId}] No search results found for: {Query}", correlationId, query);
                    return [];
                }

                var results = hits.Select(hit =>
                {
                    var title = hit?["title"]?.GetValue<string>() ?? string.Empty;
                    var snippet = hit?["snippet"]?.GetValue<string>() ?? string.Empty;
                    var pageUrl = new UriBuilder("https", "en.wikipedia.org") { Path = $"/wiki/{title.Replace(' ', '_')}" }.Uri.AbsoluteUri;
                    return new WikipediaResult(title, snippet, pageUrl);
                }).ToList();

                logger.LogInformation("[{CorrelationId}] Found {Count} results for: {Query}", correlationId, results.Count, query);
                return results;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var delay = (int)Math.Pow(2, attempt + 1);
                logger.LogWarning("[{CorrelationId}] Rate limited on attempt {Attempt}, retrying in {Delay}s", correlationId, attempt, delay);
                if (attempt == MaxRetries) return [];
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{CorrelationId}] Unexpected error searching Wikipedia for: {Query}", correlationId, query);
                return [];
            }
        }
        return [];
    }
}
