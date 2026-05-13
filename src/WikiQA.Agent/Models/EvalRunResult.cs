namespace WikiQA.Agent.Models;

public record EvalRunResult(
    string RunId,
    string SuiteName,
    DateTimeOffset RunAt,
    IReadOnlyList<EvalResult> Results,
    int PassCount,
    int FailCount,
    int IncompleteCount
);
