using System.Text.Json;
using WikiQA.Agent.Agent;
using WikiQA.Agent.Models;

namespace WikiQA.Evals;

public class EvalExecutor(string suitesDirectory, IQAAgent agent)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<EvalRunResult> RunAsync(string suiteName)
    {
        var suite = LoadSuite(suiteName);
        var results = new List<EvalResult>();

        foreach (var testCase in suite.Cases)
        {
            var response = await agent.AnswerAsync(testCase.Query);
            var sourceUrls = response.Sources.Select(s => s.PageUrl).ToList();

            var failures = testCase.ExpectedKeywords
                .Where(kw => !response.Answer.Contains(kw, StringComparison.OrdinalIgnoreCase))
                .Select(kw => $"Missing keyword: '{kw}'")
                .ToList();

            if (testCase.AnyReferencedUrls.Count > 0 &&
                !testCase.AnyReferencedUrls.Any(url => sourceUrls.Contains(url, StringComparer.OrdinalIgnoreCase)))
                failures.Add($"None of the expected URLs were referenced: {string.Join(", ", testCase.AnyReferencedUrls)}");

            results.Add(new EvalResult(
                CaseId: testCase.Id,
                Query: testCase.Query,
                ActualAnswer: response.Answer,
                ReferencedUrls: sourceUrls,
                Passed: failures.Count == 0,
                FailureReasons: failures
            ));
        }

        return new EvalRunResult(
            RunId: Guid.NewGuid().ToString(),
            SuiteName: suite.Name,
            RunAt: DateTimeOffset.UtcNow,
            Results: results,
            PassCount: results.Count(r => r.Passed),
            FailCount: results.Count(r => !r.Passed)
        );
    }

    private TestSuite LoadSuite(string suiteName)
    {
        var path = Path.Combine(suitesDirectory, $"{suiteName}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Test suite not found: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TestSuite>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize suite: {suiteName}");
    }
}
