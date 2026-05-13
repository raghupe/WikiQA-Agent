using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using WikiQA.Agent.Models;
using WikiQA.Agent.Prompts;
using WikiQA.Agent.Tools;
using WikiQA.Agent.WikiClient;

namespace WikiQA.Agent.Agent;

public class WikipediaAgent(IPromptBuilder promptBuilder, ILogger<WikipediaAgent> logger, ILoggerFactory loggerFactory) : IQAAgent
{
    private readonly AnthropicClient _client = new();
    private readonly WikipediaSearchTool _wikipediaTool = new(
        loggerFactory.CreateLogger<WikipediaSearchTool>(),
        loggerFactory.CreateLogger<WikipediaClient>());

    public async Task<AgentResponse> AnswerAsync(string query)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        logger.LogInformation("[{CorrelationId}] Query received: {Query}", correlationId, query);

        var systemPrompt = promptBuilder.Load("System", 1);
        var sources = new List<WikipediaResult>();

        var messages = new List<Message>
        {
            new() { Role = RoleType.User, Content = [new TextContent { Text = query }] }
        };

        while (true)
        {
            var parameters = new MessageParameters
            {
                Model = AnthropicModels.Claude46Sonnet,
                MaxTokens = 1024,
                System = string.IsNullOrEmpty(systemPrompt)
                    ? null
                    : [new SystemMessage(systemPrompt)],
                Tools = [WikipediaSearchTool.Definition],
                Messages = messages
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters);

            if (response.StopReason == "end_turn")
            {
                var answer = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
                logger.LogInformation("[{CorrelationId}] Final answer produced, sources: {SourceCount}", correlationId, sources.Count);
                return new AgentResponse(answer, 1, sources);
            }

            var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();
            if (toolUse is null) break;

            var question = toolUse.Input["question"]?.GetValue<string>() ?? query;
            logger.LogInformation("[{CorrelationId}] Claude requested tool call: {Question}", correlationId, question);
            var (json, result) = await _wikipediaTool.ExecuteAsync(question, correlationId);
            if (result is not null)
                sources.Add(result);

            messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content });
            messages.Add(new Message
            {
                Role = RoleType.User,
                Content =
                [
                    new ToolResultContent
                    {
                        ToolUseId = toolUse.Id,
                        Content = [new TextContent { Text = json }]
                    }
                ]
            });
        }

        logger.LogWarning("[{CorrelationId}] Agent loop exited without end_turn", correlationId);
        return new AgentResponse(string.Empty, 1, sources);
    }
}
