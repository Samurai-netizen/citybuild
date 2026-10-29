namespace CityBuilder.Api.Endpoints;

/// <summary>
/// The log messages about commands (source-generated, decision 029 in core-instructions/12-decisions.md; ids 5000–5999): how each command
/// ended, with the command type, its request id, the player and the request's duration in milliseconds (real time). Never the body.
/// </summary>
internal static partial class CommandLog
{
    /// <summary>Logs a command applied and stored now (event id 5000). The source generator writes the method's body.</summary>
    /// <param name="logger">The logger of the command endpoints.</param>
    /// <param name="command">The command type, such as <c>place-building</c>.</param>
    /// <param name="requestId">The command's request id.</param>
    /// <param name="playerId">The acting player.</param>
    /// <param name="durationMs">How long the request took so far, in milliseconds.</param>
    [LoggerMessage(EventId = 5000, Level = LogLevel.Information,
        Message = "Command {Command} {RequestId} of player {PlayerId} accepted in {DurationMs} ms")]
    public static partial void Accepted(ILogger logger, string command, Guid requestId, Guid playerId, double durationMs);

    /// <summary>
    /// Logs a command refused with an error code (event id 5001): a business rule (<c>INSUFFICIENT_RESOURCE</c>, …), a shape the pipeline
    /// refused (<c>INVALID_ARGUMENT</c>) or a request id used before for another payload (<c>IDEMPOTENCY_CONFLICT</c>). The source generator
    /// writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the command endpoints.</param>
    /// <param name="command">The command type.</param>
    /// <param name="requestId">The command's request id.</param>
    /// <param name="playerId">The acting player.</param>
    /// <param name="code">The error code of the answer.</param>
    /// <param name="durationMs">How long the request took so far, in milliseconds.</param>
    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "Command {Command} {RequestId} of player {PlayerId} rejected with {Code} in {DurationMs} ms")]
    public static partial void Rejected(ILogger logger, string command, Guid requestId, Guid playerId, string code, double durationMs);

    /// <summary>
    /// Logs a retried command answered with its stored response (event id 5002): an idempotent replay, which ran nothing. The source generator
    /// writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the command endpoints.</param>
    /// <param name="command">The command type.</param>
    /// <param name="requestId">The command's request id.</param>
    /// <param name="playerId">The acting player.</param>
    /// <param name="durationMs">How long the request took so far, in milliseconds.</param>
    [LoggerMessage(EventId = 5002, Level = LogLevel.Information,
        Message = "Command {Command} {RequestId} of player {PlayerId} replayed from its receipt in {DurationMs} ms")]
    public static partial void Replayed(ILogger logger, string command, Guid requestId, Guid playerId, double durationMs);

    /// <summary>
    /// Logs a command that ended in <c>CONCURRENCY_CONFLICT</c> (event id 5003): the city's lock was not granted in time or the city changed
    /// without it (ADR-0009, Watch). Nothing was committed; the client retries with the same request id. The source generator writes the
    /// method's body.
    /// </summary>
    /// <param name="logger">The logger of the command endpoints.</param>
    /// <param name="command">The command type.</param>
    /// <param name="requestId">The command's request id.</param>
    /// <param name="playerId">The acting player.</param>
    /// <param name="durationMs">How long the request took so far, in milliseconds.</param>
    [LoggerMessage(EventId = 5003, Level = LogLevel.Warning,
        Message = "Command {Command} {RequestId} of player {PlayerId} hit a concurrency conflict in {DurationMs} ms; nothing committed")]
    public static partial void Conflict(ILogger logger, string command, Guid requestId, Guid playerId, double durationMs);
}
