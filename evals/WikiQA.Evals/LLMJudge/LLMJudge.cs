using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using System.Text.Json.Nodes;
using WikiQA.Agent.Models;
using WikiQA.Agent.Prompts;

namespace WikiQA.Evals.LLMJudge;

public class LLMJudge(IPromptBuilder promptBuilder)
{
    private static readonly AnthropicClient Client = new();
    private static readonly string[] MetricNames =
        ["Groundedness", "Relevance", "Coherence", "Precision", "Recall", "Completeness"];

    private static readonly Anthropic.SDK.Common.Tool EvaluationTool = BuildEvaluationTool();

    public async Task<IReadOnlyList<JudgeMetric>> EvaluateAsync(
        string question, string answer, IReadOnlyList<WikipediaResult> sources)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return MetricNames.Select(m => new JudgeMetric(m, 0, "No answer provided.")).ToList();

        var judgePrompt = promptBuilder.Load("Judge", 1);

        var citedSources = sources
            .Where(s => answer.Contains(s.PageUrl, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var sourcesText = citedSources.Count > 0
            ? string.Join("\n\n", citedSources.Select(s =>
                $"Title: {s.Title}\nURL: {s.PageUrl}\nSnippet: {s.Snippet}"))
            : "No Wikipedia sources were cited in the answer.";

        var userMessage = $"""
            Question: {question}

            Agent Answer:
            {answer}

            Wikipedia Sources Cited:
            {sourcesText}
            """;

        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude45Haiku,
            MaxTokens = 2048,
            System = [new SystemMessage(judgePrompt)],
            Tools = [EvaluationTool],
            ToolChoice = new ToolChoice { Type = ToolChoiceType.Tool, Name = "submit_evaluation" },
            Messages = [new Message { Role = RoleType.User, Content = [new TextContent { Text = userMessage }] }]
        };

        var response = await Client.Messages.GetClaudeMessageAsync(parameters);
        var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();
        if (toolUse is null) return [];

        var metrics = toolUse.Input["metrics"]?.AsArray();
        if (metrics is null) return [];

        return metrics.Select(m => new JudgeMetric(
            m?["name"]?.GetValue<string>() ?? string.Empty,
            m?["score"]?.GetValue<int>() ?? 0,
            m?["reasoning"]?.GetValue<string>() ?? string.Empty
        )).ToList();
    }

    public async Task<IReadOnlyList<JudgeMetricStats>> EvaluateWithConfidenceAsync(
        string question, string answer, IReadOnlyList<WikipediaResult> sources, int runs = 3)
    {
        var allRuns = new List<IReadOnlyList<JudgeMetric>>();
        for (int i = 0; i < runs; i++)
            allRuns.Add(await EvaluateAsync(question, answer, sources));

        const double tValue = 4.303; // t(0.025, df=2) for 95% CI with n=3
        return MetricNames.Select(name =>
        {
            var scores = allRuns
                .Select(run => run.FirstOrDefault(m => m.Name == name)?.Score ?? 0)
                .ToList();
            var mean   = scores.Average();
            var stdDev = Math.Sqrt(scores.Average(s => Math.Pow(s - mean, 2)));
            var ci     = Math.Round(tValue * stdDev / Math.Sqrt(runs), 2);
            var lower  = Math.Round(Math.Max(1, mean - ci), 2);
            var upper  = Math.Round(Math.Min(5, mean + ci), 2);
            var passAtK = scores.Max();
            return new JudgeMetricStats(name, scores, Math.Round(mean, 2), Math.Round(stdDev, 2), ci, lower, upper, passAtK);
        }).ToList();
    }

    private static Anthropic.SDK.Common.Tool BuildEvaluationTool()
    {
        var schema = JsonNode.Parse("""
            {
              "type": "object",
              "properties": {
                "metrics": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "name":      { "type": "string" },
                      "score":     { "type": "integer", "minimum": 1, "maximum": 5 },
                      "reasoning": { "type": "string" }
                    },
                    "required": ["name", "score", "reasoning"]
                  }
                }
              },
              "required": ["metrics"]
            }
            """);

        return new Function("submit_evaluation",
            "Submit the evaluation scores for all metrics.",
            schema);
    }
}
