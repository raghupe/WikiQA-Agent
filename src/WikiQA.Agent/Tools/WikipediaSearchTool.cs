using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WikiQA.Agent.Models;
using WikiQA.Agent.WikiClient;

namespace WikiQA.Agent.Tools;

public class WikipediaSearchTool(ILogger<WikipediaSearchTool> logger, ILogger<WikipediaClient> clientLogger)
{
    private readonly WikipediaClient _client = new(clientLogger);

    public static Anthropic.SDK.Common.Tool Definition { get; } = BuildDefinition();

    public async Task<(string Json, IReadOnlyList<WikipediaResult> Results)> ExecuteAsync(string question, string correlationId)
    {
        logger.LogInformation("[{CorrelationId}] Tool 'wikipedia_search' invoked with: {Question}", correlationId, question);

        var results = await _client.SearchAsync(question, correlationId);

        if (results.Count == 0)
        {
            logger.LogWarning("[{CorrelationId}] Tool 'wikipedia_search' returned no results for: {Question}", correlationId, question);
            return ("No Wikipedia articles found.", []);
        }

        logger.LogInformation("[{CorrelationId}] Tool 'wikipedia_search' returned {Count} results", correlationId, results.Count);
        return (JsonSerializer.Serialize(results), results);
    }

    private static Anthropic.SDK.Common.Tool BuildDefinition()
    {
        var schema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, Property>
            {
                ["question"] = new Property
                {
                    Type = "string",
                    Description = "The question to search for on Wikipedia"
                }
            },
            Required = ["question"]
        };

        var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        return new Function(
            "wikipedia_search",
            "Search Wikipedia to find information that helps answer the question.",
            JsonNode.Parse(json));
    }
}
