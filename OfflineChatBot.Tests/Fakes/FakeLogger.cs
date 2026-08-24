using Microsoft.Extensions.Logging;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new List<LogEntry>();

        public IEnumerable<LogEntry> Problems => Entries.Where(entry => entry.Level >= LogLevel.Warning);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    public record LogEntry(LogLevel Level, string Message, Exception? Exception);
}