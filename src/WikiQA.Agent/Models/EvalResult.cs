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
    IReadOnlyList<string> FailureReasons
);
