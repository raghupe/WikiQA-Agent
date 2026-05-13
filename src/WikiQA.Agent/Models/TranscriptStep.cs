namespace WikiQA.Agent.Models;

public record TranscriptStep(
    string Type,
    string Content,
    DateTimeOffset Timestamp
);
