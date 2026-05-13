using System.Text.Json;
using WikiQA.Agent.Agent;
using WikiQA.Agent.Models;
using WikiQA.Agent.Prompts;
using WikiQA.Agent.Transcript;
using WikiQA.Evals.Heuristics;
using WikiQA.Evals.LLMJudge;
using WikiQA.Evals.Safety;
using AgentException = WikiQA.Agent.Agent.AgentException;

namespace WikiQA.Evals;

public class EvalExecutor(string suitesDirectory, IQAAgent agent, EvalResultWriter evalResultWriter, IPromptBuilder promptBuilder)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HeuristicValidator _heuristics = new();
    private readonly LLMJudge.LLMJudge _judge = new(promptBuilder);
    private readonly SafetyEvaluator _safety = new(promptBuilder);

    public async Task<EvalRunResult> RunAsync(string suiteName, Func<EvalResult, Task>? onResult = null)
    {
        var suite = LoadSuite(suiteName);
        var runId = Guid.NewGuid().ToString("N")[..8];
        var results = new List<EvalResult>();

        foreach (var testCase in suite.Cases)
        {
            var correlationId = $"{suiteName}_{testCase.Id}_{runId}_{Guid.NewGuid().ToString("N")[..8]}";
            EvalResult evalResult;

            try
            {
                var response = await agent.AnswerAsync(testCase.Query, correlationId);
                var sourceUrls = response.Sources.Select(s => s.PageUrl).ToList();
                var searchCount = response.Sources.Count;
                var isWikiUsed = searchCount > 0;

                var failures = testCase.ExpectedKeywords
                    .Where(kw => !response.Answer.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    .Select(kw => $"Missing keyword: '{kw}'")
                    .ToList();

                if (suite.EvalType.HasFlag(EvalType.Deterministic))
                {
                    if (testCase.AnyKeywords.Count > 0 &&
                        !testCase.AnyKeywords.Any(kw => response.Answer.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                        failures.Add($"None of the expected keywords found: {string.Join(", ", testCase.AnyKeywords)}");

                    foreach (var kw in testCase.ProhibitedKeywords)
                        if (response.Answer.Contains(kw, StringComparison.OrdinalIgnoreCase))
                            failures.Add($"Prohibited keyword found: '{kw}'");

                    if (testCase.AnyReferencedUrls.Count > 0 &&
                        !testCase.AnyReferencedUrls.Any(url => sourceUrls.Any(s => UrlsMatch(s, url))))
                        failures.Add($"None of the expected URLs were referenced: {string.Join(", ", testCase.AnyReferencedUrls)}");

                    foreach (var url in testCase.ExpectedReferenceUrls)
                        if (!sourceUrls.Any(s => UrlsMatch(s, url)))
                            failures.Add($"Expected URL not referenced: '{url}'");

                    if (isWikiUsed != testCase.IsWikiUseExpected)
                        failures.Add($"Wiki use mismatch: expected {testCase.IsWikiUseExpected}, actual {isWikiUsed}");

                    if (searchCount < testCase.MinExpectedSearchCount)
                        failures.Add($"Search count {searchCount} below minimum {testCase.MinExpectedSearchCount}");
                }

                var heuristicResults = new List<HeuristicResult>();
                if (suite.EvalType.HasFlag(EvalType.Heuristic))
                {
                    heuristicResults.Add(_heuristics.ValidateWikipediaUrls(sourceUrls));
                    heuristicResults.Add(_heuristics.ValidateWordOverlap(response.Answer, response.Sources));
                }

                var judgeMetrics = new List<JudgeMetricStats>();
                if (suite.EvalType.HasFlag(EvalType.LLMJudge))
                {
                    judgeMetrics.AddRange(await _judge.EvaluateWithConfidenceAsync(
                        testCase.Query, response.Answer, response.Sources));

                    var hardFail = judgeMetrics.Any(m => m.PassAtK < 2);
                    var judgePass = judgeMetrics.All(m => m.PassAtK >= 3);

                    if (hardFail)
                        failures.Add($"LLM Judge hard fail: {string.Join(", ",
                            judgeMetrics.Where(m => m.PassAtK < 2).Select(m => $"{m.Name}={m.PassAtK}"))}");
                    else if (!judgePass)
                        failures.Add($"LLM Judge fail: {string.Join(", ",
                            judgeMetrics.Where(m => m.PassAtK < 3).Select(m => $"{m.Name}={m.PassAtK}"))}");
                }

                var safetyMetrics = new List<SafetyMetricStats>();
                if (suite.EvalType.HasFlag(EvalType.Safety))
                {
                    safetyMetrics.AddRange(await _safety.EvaluateWithConfidenceAsync(
                        testCase.Query, response.Answer));

                    var safetyFailed = safetyMetrics.Where(m => !m.PassAtK).ToList();
                    if (safetyFailed.Count > 0)
                        failures.Add($"Safety fail: {string.Join(", ", safetyFailed.Select(m => m.Name))}");
                }

                evalResult = new EvalResult(
                    CaseId: testCase.Id,
                    Query: testCase.Query,
                    ActualAnswer: response.Answer,
                    ReferencedUrls: sourceUrls,
                    SearchCount: searchCount,
                    IsWikiUsed: isWikiUsed,
                    IsWikiUseExpected: testCase.IsWikiUseExpected,
                    TranscriptPath: response.TranscriptPath,
                    Status: failures.Count == 0 ? EvalStatus.Passed : EvalStatus.Failed,
                    HeuristicResults: heuristicResults,
                    JudgeMetrics: judgeMetrics,
                    SafetyMetrics: safetyMetrics,
                    FailureReasons: failures
                );
            }
            catch (AgentException ex)
            {
                var allMessages = $"{ex.Message} {ex.InnerException?.Message}";
                var reason = allMessages switch
                {
                    var m when m.Contains("tool_use") || m.Contains("tool_result")                                             => "ToolFailure",
                    var m when m.Contains("overloaded") || m.Contains("529")                                                   => "ModelFailure",
                    var m when m.Contains("403") || m.Contains("429") || m.Contains("TooManyRequests") || m.Contains("throttl") => "WikiFailure",
                    _                                                                                                           => "SystemFailure"
                };

                evalResult = new EvalResult(
                    CaseId: testCase.Id,
                    Query: testCase.Query,
                    ActualAnswer: string.Empty,
                    ReferencedUrls: [],
                    SearchCount: 0,
                    IsWikiUsed: false,
                    IsWikiUseExpected: testCase.IsWikiUseExpected,
                    TranscriptPath: ex.TranscriptPath,
                    Status: EvalStatus.Incomplete,
                    HeuristicResults: [],
                    JudgeMetrics: new List<JudgeMetricStats>(),
                    SafetyMetrics: new List<SafetyMetricStats>(),
                    FailureReasons: [$"System failure: {reason} | {ex.Message}{(ex.InnerException != null ? " | " + ex.InnerException.Message : "")}"]
                );
            }
            catch (Exception ex)
            {
                evalResult = new EvalResult(
                    CaseId: testCase.Id,
                    Query: testCase.Query,
                    ActualAnswer: string.Empty,
                    ReferencedUrls: [],
                    SearchCount: 0,
                    IsWikiUsed: false,
                    IsWikiUseExpected: testCase.IsWikiUseExpected,
                    TranscriptPath: string.Empty,
                    Status: EvalStatus.Incomplete,
                    HeuristicResults: [],
                    JudgeMetrics: new List<JudgeMetricStats>(),
                    SafetyMetrics: new List<SafetyMetricStats>(),
                    FailureReasons: [$"System failure: {ex.Message}{(ex.InnerException != null ? " | " + ex.InnerException.Message : "")}"]
                );
            }

            results.Add(evalResult);
            if (onResult is not null)
                await onResult(evalResult);
        }

        var evalRunResult = new EvalRunResult(
            RunId: runId,
            SuiteName: suite.Name,
            RunAt: DateTimeOffset.UtcNow,
            Results: results,
            PassCount: results.Count(r => r.Status == EvalStatus.Passed),
            FailCount: results.Count(r => r.Status == EvalStatus.Failed),
            IncompleteCount: results.Count(r => r.Status == EvalStatus.Incomplete)
        );

        await evalResultWriter.WriteAsync(evalRunResult, suiteName);
        return evalRunResult;
    }

    private static bool UrlsMatch(string a, string b) =>
        Uri.TryCreate(a, UriKind.Absolute, out var uriA) &&
        Uri.TryCreate(b, UriKind.Absolute, out var uriB) &&
        Uri.Compare(uriA, uriB, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;

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
