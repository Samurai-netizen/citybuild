using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CityBuilder.IntegrationTests.Logging;

/// <summary>
/// A logger provider that keeps what the server under test logs, with what the JSON console writes of each line: the level, the category,
/// the event id, the formatted message, the template's named values, the values of every scope the line was written in (the request's
/// trace id among them) and the exception. A test checks the log lines an operator would read. Add it to a server with <see cref="AddTo"/>.
/// The configured levels apply as to every provider (<c>Logging:LogLevel</c>).
/// </summary>
internal sealed class CapturingLogger : ILoggerProvider, ISupportExternalScope
{
    /// <summary>One log entry.</summary>
    /// <param name="Level">The level.</param>
    /// <param name="Category">The logger's category.</param>
    /// <param name="EventId">The event id; 0 when the line has none.</param>
    /// <param name="Message">The formatted message.</param>
    /// <param name="Values">The template's named values, by name (without the template itself).</param>
    /// <param name="Scopes">The named values of the scopes the line was written in, outermost first; a name can repeat.</param>
    /// <param name="Exception">The exception logged with it, or <see langword="null"/>.</param>
    public sealed record Entry(
        LogLevel Level,
        string Category,
        int EventId,
        string Message,
        IReadOnlyDictionary<string, object?> Values,
        IReadOnlyList<KeyValuePair<string, object?>> Scopes,
        Exception? Exception)
    {
        /// <summary>The value of a scope, the innermost one if several scopes name it, as text.</summary>
        /// <param name="name">The scope value's name, such as <c>TraceId</c>.</param>
        /// <returns>The value, or <see langword="null"/> when no scope names it.</returns>
        public string? Scope(string name) =>
            Scopes.LastOrDefault(pair => pair.Key == name).Value?.ToString();

        /// <summary>Everything the line holds as text (message, values, scopes, exception), for checks that something is in none of it.</summary>
        /// <returns>The text.</returns>
        public string AllText() =>
            string.Join(
                '\n',
                new[] { Category, Message, Exception?.ToString() ?? string.Empty }
                    .Concat(Values.Select(pair => pair.Key + "=" + pair.Value))
                    .Concat(Scopes.Select(pair => pair.Key + "=" + pair.Value)));
    }

    /// <summary>Everything logged so far.</summary>
    private readonly ConcurrentQueue<Entry> _entries = new();

    /// <summary>The scopes of the logging system, set by the logger factory; the requests' scopes (trace id, path) are in it.</summary>
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    /// <summary>A snapshot of everything logged so far, in order.</summary>
    public IReadOnlyList<Entry> Entries => [.. _entries];

    /// <summary>The entries of one event id, in order.</summary>
    /// <param name="eventId">The event id.</param>
    /// <returns>The entries.</returns>
    public IReadOnlyList<Entry> Events(int eventId) => [.. _entries.Where(entry => entry.EventId == eventId)];

    /// <summary>A copy of the server that logs to this provider too.</summary>
    /// <param name="factory">The server.</param>
    /// <returns>The copy; the caller disposes it.</returns>
    public WebApplicationFactory<Program> AddTo(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(host => host.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(this)));

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Capture(categoryName, this);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>The named values of a log state or a scope: its key-value pairs, without the template (<c>{OriginalFormat}</c>).</summary>
    /// <param name="state">The state or scope object.</param>
    /// <returns>The pairs; none for a state that has no names.</returns>
    private static IEnumerable<KeyValuePair<string, object?>> Named(object? state) =>
        state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.Where(pair => pair.Key != "{OriginalFormat}")
            : [];

    /// <summary>The logger of one category.</summary>
    /// <param name="category">The category.</param>
    /// <param name="owner">The provider that keeps the entries and the scopes.</param>
    private sealed class Capture(string category, CapturingLogger owner) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => owner._scopes.Push(state);

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<KeyValuePair<string, object?>>();
            owner._scopes.ForEachScope((scope, list) => list.AddRange(Named(scope)), scopes);
            var values = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, object?> pair in Named(state))
            {
                values[pair.Key] = pair.Value;
            }

            owner._entries.Enqueue(new Entry(logLevel, category, eventId.Id, formatter(state, exception), values, scopes, exception));
        }
    }
}
