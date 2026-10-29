using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CityBuilder.IntegrationTests.Errors;

/// <summary>
/// A logger provider that keeps what the server under test logs, so a test can check that an exception the answer says nothing about is in
/// the log (the operators' place for it). Add it to a server with <see cref="AddTo"/>.
/// </summary>
internal sealed class CapturingLogger : ILoggerProvider
{
    /// <summary>One log entry: its level, the category (the logging class), the formatted message and the exception, if any.</summary>
    /// <param name="Level">The level.</param>
    /// <param name="Category">The logger's category.</param>
    /// <param name="Message">The formatted message.</param>
    /// <param name="Exception">The exception logged with it, or <see langword="null"/>.</param>
    public sealed record Entry(LogLevel Level, string Category, string Message, Exception? Exception);

    /// <summary>Everything logged so far.</summary>
    private readonly ConcurrentQueue<Entry> _entries = new();

    /// <summary>A snapshot of everything logged so far, in order.</summary>
    public IReadOnlyList<Entry> Entries => [.. _entries];

    /// <summary>A copy of the server that logs to this provider too.</summary>
    /// <param name="factory">The server.</param>
    /// <returns>The copy; the caller disposes it.</returns>
    public WebApplicationFactory<Program> AddTo(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(host => host.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(this)));

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Capture(categoryName, _entries);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>The logger of one category.</summary>
    /// <param name="category">The category.</param>
    /// <param name="entries">Where the entries go.</param>
    private sealed class Capture(string category, ConcurrentQueue<Entry> entries) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new Entry(logLevel, category, formatter(state, exception), exception));
    }
}
