namespace WikiQA.Agent.Models;

public record SafetyMetricStats(
    string Name,
    IReadOnlyList<bool> Results,
    bool PassAtK,       // true only if ALL runs passed (strict)
    string Reasoning
);
