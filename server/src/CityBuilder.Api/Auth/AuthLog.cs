namespace CityBuilder.Api.Auth;

/// <summary>
/// The log messages about authentication (source-generated, decision 029 in core-instructions/12-decisions.md; ids 4000–4999). They name the
/// player and the kind of a refusal, never a token, a signing key or a display name (core-instructions/07-security-model.md, logging policy).
/// </summary>
internal static partial class AuthLog
{
    /// <summary>Logs a dev login that issued a token (event id 4000). The source generator writes the method's body.</summary>
    /// <param name="logger">The logger of the endpoint.</param>
    /// <param name="playerId">The logged-in player.</param>
    /// <param name="isNewPlayer">Whether this login created the player and their starter city.</param>
    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Dev login of player {PlayerId} (new player: {IsNewPlayer})")]
    public static partial void DevLoginSucceeded(ILogger logger, Guid playerId, bool isNewPlayer);

    /// <summary>
    /// Logs a dev login refused for its body: no body, or a display name that isn't allowed (event id 4001). The name itself is not logged.
    /// The source generator writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the endpoint.</param>
    /// <param name="code">The error code of the answer.</param>
    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Dev login refused with {Code}")]
    public static partial void DevLoginRefused(ILogger logger, string code);

    /// <summary>
    /// Logs a request whose access token the bearer handler accepted (event id 4002; Debug, because every game request has one). The source
    /// generator writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the bearer events.</param>
    /// <param name="playerId">The token's player.</param>
    [LoggerMessage(EventId = 4002, Level = LogLevel.Debug, Message = "Access token accepted for player {PlayerId}")]
    public static partial void TokenAccepted(ILogger logger, Guid playerId);

    /// <summary>
    /// Logs a request refused with 401 because it had no working token (event id 4003). The reason is <c>NoToken</c> or the type name of the
    /// token's validation failure (such as <c>SecurityTokenInvalidLifetimeException</c> for an expired one); the answer never says it
    /// (<c>05</c>), and neither the token nor the failure's message is logged. The source generator writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the bearer events.</param>
    /// <param name="method">The request's HTTP method.</param>
    /// <param name="path">The request's path; no query string.</param>
    /// <param name="reason">Why no player was authenticated.</param>
    [LoggerMessage(EventId = 4003, Level = LogLevel.Warning, Message = "Request {Method} {Path} refused without a working access token: {Reason}")]
    public static partial void TokenRefused(ILogger logger, string method, string path, string reason);
}
