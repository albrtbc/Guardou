using Microsoft.Extensions.Logging;

namespace Guardou.UnitTests.Files;

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public List<LogRecord> Records { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        Entries.Add((logLevel, message));
        Records.Add(new LogRecord(
            logLevel,
            eventId,
            message,
            (state as IEnumerable<KeyValuePair<string, object?>> ?? []).ToDictionary(pair => pair.Key, pair => pair.Value)));
    }
}

internal sealed record LogRecord(LogLevel Level, EventId EventId, string Message, IReadOnlyDictionary<string, object?> Properties);
