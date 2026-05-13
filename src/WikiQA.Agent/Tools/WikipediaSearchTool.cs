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

    public async Task<(string Json, WikipediaResult? Result)> ExecuteAsync(string question, string correlationId)
    {
        logger.LogInformation("[{CorrelationId}] Tool 'wikipedia_search' invoked with: {Question}", correlationId, question);

        var result = await _client.SearchAsync(question, correlationId);

        if (result is null)
        {
            logger.LogWarning("[{CorrelationId}] Tool 'wikipedia_search' returned no result for: {Question}", correlationId, question);
            return ("No Wikipedia article found.", null);
        }

        logger.LogInformation("[{CorrelationId}] Tool 'wikipedia_search' succeeded: {Title}", correlationId, result.Title);
        return (JsonSerializer.Serialize(result), result);
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
