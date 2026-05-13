namespace WikiQA.Agent.Models;

public record SafetyMetric(
    string Name,
    bool Passed,
    string Reasoning
);
