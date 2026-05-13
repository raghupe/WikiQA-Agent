namespace WikiQA.Agent.Models;

public record JudgeMetricStats(
    string Name,
    IReadOnlyList<int> Scores,
    double Mean,
    double StdDev,
    double ConfidenceInterval,
    double LowerBound,
    double UpperBound,
    int PassAtK
);
