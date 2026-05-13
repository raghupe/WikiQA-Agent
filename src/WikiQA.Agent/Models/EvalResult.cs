namespace WikiQA.Agent.Models;

public record EvalResult(
    string CaseId,
    string Query,
    string ActualAnswer,
    IReadOnlyList<string> ReferencedUrls,
    bool Passed,
    IReadOnlyList<string> FailureReasons
);
