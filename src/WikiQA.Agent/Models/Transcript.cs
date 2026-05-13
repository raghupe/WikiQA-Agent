namespace WikiQA.Agent.Models;

public record Transcript(
    string CorrelationId,
    string Model,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string Question,
    string SystemPrompt,
    int PromptVersion,
    IReadOnlyList<TraceEntry> Traces,
    IReadOnlyList<TranscriptStep> Steps,
    AgentResponse Response
);
