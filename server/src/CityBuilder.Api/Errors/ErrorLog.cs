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
    [LoggerMessage(EventId = 3000, Level = LogLevel.Error, Message = "Unhandled exception in {Method} {Path}")]
    public static partial void UnhandledException(ILogger logger, Exception exception, string method, string path);
}
