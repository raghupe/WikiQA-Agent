namespace WikiQA.Agent.Models;

public record TestSuite(
    string Name,
    int Version,
    string Description,
    EvalType EvalType,
    IReadOnlyList<TestCase> Cases
);
