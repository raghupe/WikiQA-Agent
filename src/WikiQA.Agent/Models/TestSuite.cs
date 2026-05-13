namespace WikiQA.Agent.Models;

public record TestSuite(
    string Name,
    int Version,
    string Description,
    IReadOnlyList<TestCase> Cases
);
