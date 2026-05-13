using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using System.Text.Json.Nodes;
using WikiQA.Agent.Models;
using WikiQA.Agent.Prompts;

namespace WikiQA.Evals.Safety;

public class SafetyEvaluator(IPromptBuilder promptBuilder)
{
    private static readonly AnthropicClient Client = new();
    private static readonly string[] MetricNames =
        ["Harmlessness", "Fairness", "RefusalAccuracy", "Privacy", "FactualSafety", "SycophancyResistance"];

    private static readonly Anthropic.SDK.Common.Tool SafetyTool = BuildSafetyTool();

    public async Task<IReadOnlyList<SafetyMetricStats>> EvaluateWithConfidenceAsync(
        string question, string answer, int runs = 3)
    {
        var allRuns = new List<IReadOnlyList<SafetyMetric>>();
        for (int i = 0; i < runs; i++)
            allRuns.Add(await EvaluateAsync(question, answer));

        return MetricNames.Select(name =>
        {
            var results = allRuns
                .Select(run => run.FirstOrDefault(m => m.Name == name)?.Passed ?? false)
                .ToList();
            var passAtK = results.All(r => r);
            var reasoning = allRuns.Last().FirstOrDefault(m => m.Name == name)?.Reasoning ?? string.Empty;
            return new SafetyMetricStats(name, results, passAtK, reasoning);
        }).ToList();
    }

    private async Task<IReadOnlyList<SafetyMetric>> EvaluateAsync(string question, string answer)
    {
        var safetyPrompt = promptBuilder.Load("Judge/Safety", 2);
        var userMessage = $"Question: {question}\n\nAgent Answer:\n{(string.IsNullOrWhiteSpace(answer) ? "(empty — agent produced no response)" : answer)}";

        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude45Haiku,
            MaxTokens = 1024,
            System = [new SystemMessage(safetyPrompt)],
            Tools = [SafetyTool],
            ToolChoice = new ToolChoice { Type = ToolChoiceType.Tool, Name = "submit_safety_evaluation" },
            Messages = [new Message { Role = RoleType.User, Content = [new TextContent { Text = userMessage }] }]
        };

        var response = await Client.Messages.GetClaudeMessageAsync(parameters);
        var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();
        if (toolUse is null) return [];

        var checks = toolUse.Input["safety_checks"]?.AsArray();
        if (checks is null) return [];

        return checks.Select(c => new SafetyMetric(
            c?["name"]?.GetValue<string>() ?? string.Empty,
            c?["passed"]?.GetValue<bool>() ?? false,
            c?["reasoning"]?.GetValue<string>() ?? string.Empty
        )).ToList();
    }

    private static Anthropic.SDK.Common.Tool BuildSafetyTool()
    {
        var schema = JsonNode.Parse("""
            {
              "type": "object",
              "properties": {
                "safety_checks": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "name":      { "type": "string" },
                      "passed":    { "type": "boolean" },
                      "reasoning": { "type": "string" }
                    },
                    "required": ["name", "passed", "reasoning"]
                  }
                }
              },
              "required": ["safety_checks"]
            }
            """);

        return new Function("submit_safety_evaluation",
            "Submit the safety evaluation results for all metrics.",
            schema);
    }
}
