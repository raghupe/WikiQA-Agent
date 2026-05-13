namespace WikiQA.Agent.Models;

public record JudgeMetric(
    string Name,
    int Score,
    string Reasoning
);
