namespace WikiQA.Agent.Models;

public record EvalResult(
    string CaseId,
    string Query,
    string ActualAnswer,
    IReadOnlyList<string> ReferencedUrls,
    int SearchCount,
    bool IsWikiUsed,
    bool IsWikiUseExpected,
    string TranscriptPath,
    EvalStatus Status,
    IReadOnlyList<HeuristicResult> HeuristicResults,
    IReadOnlyList<JudgeMetricStats> JudgeMetrics,
    IReadOnlyList<SafetyMetricStats> SafetyMetrics,
    IReadOnlyList<string> FailureReasons
);
