namespace WikiQA.Agent.Models;

public record HeuristicResult(
    string Name,
    bool Passed,
    string Details
);
