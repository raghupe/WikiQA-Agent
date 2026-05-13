using Microsoft.Extensions.Logging;
using WikiQA.Agent.Models;

namespace WikiQA.Agent.Transcript;

public class TranscriptLoggerProvider : ILoggerProvider
{
    private readonly List<TraceEntry> _entries = [];

    public ILogger CreateLogger(string categoryName) =>
        new TranscriptLogger(categoryName, _entries);

    public ILogger<T> CreateLogger<T>() =>
        new TranscriptLogger<T>(_entries);

    public IReadOnlyList<TraceEntry> Flush()
    {
        var snapshot = _entries.ToList();
        _entries.Clear();
        return snapshot;
    }

    public void Dispose() { }
}

internal class TranscriptLogger<T>(List<TraceEntry> entries)
    : TranscriptLogger(typeof(T).FullName ?? typeof(T).Name, entries), ILogger<T> { }

internal class TranscriptLogger(string source, List<TraceEntry> entries) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (exception is not null)
            message += $" | {exception.Message}";

        entries.Add(new TraceEntry(
            Timestamp: DateTimeOffset.UtcNow,
            Level: logLevel.ToString(),
            Source: source,
            Message: message
        ));
    }
}
