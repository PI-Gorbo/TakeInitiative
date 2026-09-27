using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>One log record the API wrote, as a test reads it.</summary>
public record LoggedRecord(LogLevel Level, string Category, string Message);

/// <summary>
/// Keeps every log record the API writes, so a test can assert that something which should never be
/// logged was not. ⌘K's belt-and-braces re-check (17a.6) logs an error and fails the request when a
/// SQL visibility fragment and the C# rule it mirrors have drifted, and normal operation must never
/// reach it: an assertion on the response cannot see that, because a search that logged nothing and
/// a search that logged an error look the same from outside.
/// </summary>
public sealed class CapturingLoggerProvider(ConcurrentQueue<LoggedRecord> records) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, records);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, ConcurrentQueue<LoggedRecord> records) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => records.Enqueue(new LoggedRecord(logLevel, category, formatter(state, exception)));
    }
}
