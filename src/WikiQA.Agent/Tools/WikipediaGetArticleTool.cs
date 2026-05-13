using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WikiQA.Agent.WikiClient;

namespace WikiQA.Agent.Tools;

public class WikipediaGetArticleTool(ILogger<WikipediaGetArticleTool> logger, ILogger<WikipediaClient> clientLogger)
{
    private readonly WikipediaClient _client = new(clientLogger);

    public static Anthropic.SDK.Common.Tool Definition { get; } = BuildDefinition();

    public async Task<string> ExecuteAsync(string url, string correlationId)
    {
        logger.LogInformation("[{CorrelationId}] Tool 'wikipedia_get_article' invoked for: {Url}", correlationId, url);
        var content = await _client.GetArticleAsync(url, correlationId);
        logger.LogInformation("[{CorrelationId}] Tool 'wikipedia_get_article' returned {Length} chars", correlationId, content.Length);
        return content;
    }

    private static Anthropic.SDK.Common.Tool BuildDefinition()
    {
        var schema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, Property>
            {
                ["url"] = new Property
                {
                    Type = "string",
                    Description = "The Wikipedia article URL to fetch full content from"
                }
            },
            Required = ["url"]
        };

        var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        return new Function("wikipedia_get_article",
            "Fetch the full introduction of a specific Wikipedia article by URL. Use this after wikipedia_search to get complete content from the most relevant result.",
            JsonNode.Parse(json));
    }
}
