namespace CityBuilder.Api.Errors;

/// <summary>The log messages about errors (source-generated, decision 029 in core-instructions/12-decisions.md).</summary>
internal static partial class ErrorLog
{
    /// <summary>
    /// Logs an unexpected exception that ended a request as <c>INTERNAL_ERROR</c> (event id 3000; ids 3000–3999 belong to error and request
    /// messages). The framework logs nothing for an exception a handler answers, so this is the only record of it; the stack trace is in the
    /// exception, never in the response. The source generator writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the handler.</param>
    /// <param name="exception">What was thrown.</param>
    /// <param name="method">The request's HTTP method.</param>
    /// <param name="path">The request's path; no query string, headers or body.</param>
    /// <param name="playerId">The player of the request's token, or <see langword="null"/> when it had none.</param>
    [LoggerMessage(EventId = 3000, Level = LogLevel.Error, Message = "Unhandled exception in {Method} {Path} for player {PlayerId}")]
    public static partial void UnhandledException(ILogger logger, Exception exception, string method, string path, Guid? playerId);

    /// <summary>
    /// Logs a request the rate limiter refused with <c>RATE_LIMITED</c> (event id 3001), with the limit class and the player. A request
    /// counted by address (the dev login) names no player, and the address is never logged (personal data,
    /// core-instructions/07-security-model.md). The source generator writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the limiter.</param>
    /// <param name="limitClass">The limit the request counted against.</param>
    /// <param name="method">The request's HTTP method.</param>
    /// <param name="path">The request's path; no query string.</param>
    /// <param name="playerId">The player of the request's token, or <see langword="null"/> when it had none.</param>
    /// <param name="retryAfterSeconds">The <c>Retry-After</c> the answer carries.</param>
    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "Rate limit {LimitClass} hit by {Method} {Path} for player {PlayerId}; retry after {RetryAfterSeconds} s")]
    public static partial void RateLimited(ILogger logger, string limitClass, string method, string path, Guid? playerId, int retryAfterSeconds);

    /// <summary>
    /// Logs a <c>CONCURRENCY_CONFLICT</c> the exception handler answered (event id 3002): the city's lock was not granted in time, or the city
    /// changed without it (ADR-0009), outside a command (a command logs its own, <c>CommandLog</c>). Nothing was committed; the client
    /// retries. The source generator writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the handler.</param>
    /// <param name="method">The request's HTTP method.</param>
    /// <param name="path">The request's path; no query string.</param>
    /// <param name="playerId">The player of the request's token, or <see langword="null"/> when it had none.</param>
    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "Concurrency conflict in {Method} {Path} for player {PlayerId}")]
    public static partial void ConcurrencyConflict(ILogger logger, string method, string path, Guid? playerId);
}
