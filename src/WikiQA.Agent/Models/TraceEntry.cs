namespace WikiQA.Agent.Models;

public record TraceEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Source,
    string Message
);
