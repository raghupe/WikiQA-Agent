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
    private readonly WikipediaGetArticleTool _getArticleTool = new(
        transcriptLogger.CreateLogger<WikipediaGetArticleTool>(),
        transcriptLogger.CreateLogger<WikipediaClient>());
    private const int MaxSearchCount = 3;

    public async Task<AgentResponse> AnswerAsync(string query, string? correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString("N")[..8];
        var startedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("[{CorrelationId}] Query received: {Query}", correlationId, query);

        var systemPrompt = promptBuilder.Load("System", 9);
        var sources = new List<WikipediaResult>();
        var steps = new List<TranscriptStep>();
        var searchCount = 0;
        var transcriptPath = Path.Combine(transcriptWriter.TranscriptsDirectory, $"{correlationId}.json");

        var messages = new List<Message>
        {
            new() { Role = RoleType.User, Content = [new TextContent { Text = query }] }
        };

        try
        {
            while (true)
            {
                var parameters = new MessageParameters
                {
                    Model = AnthropicModels.Claude46Sonnet,
                    MaxTokens = 4096,
                    System = string.IsNullOrEmpty(systemPrompt)
                        ? null
                        : [new SystemMessage(systemPrompt)],
                    Tools = searchCount < MaxSearchCount
                        ? [WikipediaSearchTool.Definition, WikipediaGetArticleTool.Definition]
                        : null,
                    Messages = messages
                };

                var response = await _client.Messages.GetClaudeMessageAsync(parameters);

                if (response.StopReason == "end_turn")
                {
                    var answer = StripThinking(response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty);
                    _logger.LogInformation("[{CorrelationId}] Final answer produced, sources: {SourceCount}", correlationId, sources.Count);

                    var agentResponse = new AgentResponse(answer, 9, sources, transcriptPath);
                    var traces = transcriptLogger.Flush();
                    await transcriptWriter.WriteAsync(new Models.Transcript(
                        CorrelationId: correlationId,
                        Model: AnthropicModels.Claude46Sonnet,
                        StartedAt: startedAt,
                        CompletedAt: DateTimeOffset.UtcNow,
                        Question: query,
                        SystemPrompt: systemPrompt,
                        PromptVersion: 9,
                        Traces: traces,
                        Steps: steps,
                        Response: agentResponse));

                    return agentResponse;
                }

                var toolUses = response.Content.OfType<ToolUseContent>().ToList();
                if (toolUses.Count == 0) break;

                var toolResults = new List<ContentBase>();
                foreach (var toolUse in toolUses)
                {
                    if (toolUse.Name == "wikipedia_get_article")
                    {
                        var url = toolUse.Input["url"]?.GetValue<string>() ?? string.Empty;
                        _logger.LogInformation("[{CorrelationId}] Claude fetching article: {Url}", correlationId, url);
                        steps.Add(new TranscriptStep("GetArticle", url, DateTimeOffset.UtcNow));
                        var content = await _getArticleTool.ExecuteAsync(url, correlationId);
                        steps.Add(new TranscriptStep("ArticleContent", content[..Math.Min(500, content.Length)], DateTimeOffset.UtcNow));
                        toolResults.Add(new ToolResultContent { ToolUseId = toolUse.Id, Content = [new TextContent { Text = content }] });
                        continue;
                    }

                    var question = toolUse.Input["question"]?.GetValue<string>() ?? query;
                    _logger.LogInformation("[{CorrelationId}] Claude requested tool call: {Question}", correlationId, question);
                    steps.Add(new TranscriptStep("ToolCall", question, DateTimeOffset.UtcNow));

                    var (json, results) = await _wikipediaTool.ExecuteAsync(question, correlationId);
                    steps.Add(new TranscriptStep("ToolResult", json, DateTimeOffset.UtcNow));
                    sources.AddRange(results);
                    searchCount++;

                    toolResults.Add(new ToolResultContent
                    {
                        ToolUseId = toolUse.Id,
                        Content = [new TextContent { Text = json }]
                    });
                }

                messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content });

                if (searchCount >= MaxSearchCount)
                    toolResults.Add(new TextContent
                    {
                        Text = "Search limit reached. Using only the information retrieved above, provide your final answer now."
                    });

                messages.Add(new Message { Role = RoleType.User, Content = toolResults });

                await Task.Delay(3000);
            }

            _logger.LogWarning("[{CorrelationId}] Agent loop exited without end_turn", correlationId);
            var fallbackResponse = new AgentResponse(string.Empty, 9, sources, transcriptPath);
            var fallbackTraces = transcriptLogger.Flush();
            await transcriptWriter.WriteAsync(new Models.Transcript(
                CorrelationId: correlationId,
                Model: AnthropicModels.Claude46Sonnet,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow,
                Question: query,
                SystemPrompt: systemPrompt,
                PromptVersion: 9,
                Traces: fallbackTraces,
                Steps: steps,
                Response: fallbackResponse));

            return fallbackResponse;
        }
        catch (Exception ex)
        {
            var traces = transcriptLogger.Flush();
            await transcriptWriter.WriteAsync(new Models.Transcript(
                CorrelationId: correlationId,
                Model: AnthropicModels.Claude46Sonnet,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow,
                Question: query,
                SystemPrompt: systemPrompt,
                PromptVersion: 9,
                Traces: traces,
                Steps: steps,
                Response: new AgentResponse(string.Empty, 9, sources, transcriptPath)));
            throw new AgentException(ex.Message, transcriptPath, ex);
        }
    }

    private static string StripThinking(string text) =>
        System.Text.RegularExpressions.Regex.Replace(
            text, @"<thinking>[\s\S]*?</thinking>", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
}
