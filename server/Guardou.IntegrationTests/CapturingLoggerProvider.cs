using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Guardou.IntegrationTests;

internal sealed record CapturedLog(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties)
{
    public IEnumerable<string> Texts => Properties.Values.Select(value => value?.ToString() ?? string.Empty).Append(Message);
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _logs = new();

    public IReadOnlyList<CapturedLog> Logs => [.. _logs];

    public void Clear()
    {
        _logs.Clear();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(categoryName, _logs);
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            logs.Enqueue(new CapturedLog(
                category,
                logLevel,
                eventId,
                formatter(state, exception),
                (state as IEnumerable<KeyValuePair<string, object?>> ?? []).ToDictionary(pair => pair.Key, pair => pair.Value)));
        }
    }
}
