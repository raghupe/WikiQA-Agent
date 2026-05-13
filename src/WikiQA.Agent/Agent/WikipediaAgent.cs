using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using WikiQA.Agent.Models;
using WikiQA.Agent.Prompts;
using WikiQA.Agent.Tools;
using WikiQA.Agent.Transcript;
using WikiQA.Agent.WikiClient;

namespace WikiQA.Agent.Agent;

public class WikipediaAgent(IPromptBuilder promptBuilder, TranscriptLoggerProvider transcriptLogger, TranscriptWriter transcriptWriter) : IQAAgent
{
    private readonly AnthropicClient _client = new();
    private readonly ILogger<WikipediaAgent> _logger = transcriptLogger.CreateLogger<WikipediaAgent>();
    private readonly WikipediaSearchTool _wikipediaTool = new(
        transcriptLogger.CreateLogger<WikipediaSearchTool>(),
        transcriptLogger.CreateLogger<WikipediaClient>());

    public async Task<AgentResponse> AnswerAsync(string query, string? correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString("N")[..8];
        var startedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("[{CorrelationId}] Query received: {Query}", correlationId, query);

        var systemPrompt = promptBuilder.Load("System", 1);
        var sources = new List<WikipediaResult>();
        var steps = new List<TranscriptStep>();
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
                _logger.LogInformation("[{CorrelationId}] Final answer produced, sources: {SourceCount}", correlationId, sources.Count);

                var agentResponse = new AgentResponse(answer, 1, sources);
                var traces = transcriptLogger.Flush();
                var transcript = new Models.Transcript(
                    CorrelationId: correlationId,
                    Model: AnthropicModels.Claude46Sonnet,
                    StartedAt: startedAt,
                    CompletedAt: DateTimeOffset.UtcNow,
                    Question: query,
                    SystemPrompt: systemPrompt,
                    PromptVersion: 1,
                    Traces: traces,
                    Steps: steps,
                    Response: agentResponse);

                await transcriptWriter.WriteAsync(transcript);
                return agentResponse;
            }

            var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();
            if (toolUse is null) break;

            var question = toolUse.Input["question"]?.GetValue<string>() ?? query;
            _logger.LogInformation("[{CorrelationId}] Claude requested tool call: {Question}", correlationId, question);
            steps.Add(new TranscriptStep("ToolCall", question, DateTimeOffset.UtcNow));

            var (json, result) = await _wikipediaTool.ExecuteAsync(question, correlationId);
            steps.Add(new TranscriptStep("ToolResult", json, DateTimeOffset.UtcNow));

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

        _logger.LogWarning("[{CorrelationId}] Agent loop exited without end_turn", correlationId);
        var fallbackResponse = new AgentResponse(string.Empty, 1, sources);
        var fallbackTraces = transcriptLogger.Flush();
        var fallbackTranscript = new Models.Transcript(
            CorrelationId: correlationId,
            Model: AnthropicModels.Claude46Sonnet,
            StartedAt: startedAt,
            CompletedAt: DateTimeOffset.UtcNow,
            Question: query,
            SystemPrompt: systemPrompt,
            PromptVersion: 1,
            Traces: fallbackTraces,
            Steps: steps,
            Response: fallbackResponse);

        await transcriptWriter.WriteAsync(fallbackTranscript);
        return fallbackResponse;
    }
}
