using CityBuilder.Api.Auth;
using CityBuilder.Api.Errors;
using CityBuilder.Application.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace CityBuilder.Api.Http;

/// <summary>
/// The global exception handler (core-instructions/05-api-contract.md, errors; <c>07</c>, information leakage): turns an exception that
/// escaped an endpoint into the catalogue's Problem Details, so no request ends in an empty body, the developer exception page (which
/// echoes the request headers, the access token included) or a stack trace. A <see cref="ConcurrencyConflictException"/> is
/// <c>CONCURRENCY_CONFLICT</c> (409, safe to retry); a <see cref="CityNotFoundException"/>, a token of a player without a city, is
/// <c>AUTHENTICATION_REQUIRED</c> (401) like every other token that can't be used, so the client logs in again; a
/// <see cref="BadHttpRequestException"/>, which the minimal API throws in every environment for a body that doesn't bind
/// (<c>RouteHandlerOptions.ThrowOnBadRequest</c>) and Kestrel for a body past its limit, is <c>INVALID_ARGUMENT</c> (400), or
/// <c>UNSUPPORTED_MEDIA_TYPE</c> (415) for a body without a content type; anything else is <c>INTERNAL_ERROR</c> (500). The response never
/// holds a stack trace; outside Development it holds no exception text either; an unexpected exception is logged with its stack
/// (<see cref="ErrorLog"/>), because the framework logs nothing for an exception a handler answers, a conflict is logged as a warning, and a
/// token of a player without a city as a refused token. A body that doesn't bind is not logged: the answer says what is wrong, and the
/// framework logs it at Debug.
/// </summary>
/// <param name="environment">Tells whether the exception's type and message may go into the answer (Development only).</param>
/// <param name="limits">The body limit, named in the answer for a body that is too large.</param>
/// <param name="logger">Where an unexpected exception is logged.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
internal sealed class ApiExceptionHandler(
    IHostEnvironment environment, IOptions<RequestLimitOptions> limits, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>The reason in the log line of a refused token (<see cref="AuthLog.TokenRefused"/>) when its player has no city.</summary>
    private const string _noCityReason = "NoCity";

    /// <summary>Where an unexpected exception is logged.</summary>
    private readonly ILogger<ApiExceptionHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Whether the exception's type and message may go into the answer.</summary>
    private readonly bool _showExceptionText = (environment ?? throw new ArgumentNullException(nameof(environment))).IsDevelopment();

    /// <summary>The largest body the server reads, in bytes.</summary>
    private readonly int _maxBodyBytes = (limits ?? throw new ArgumentNullException(nameof(limits))).Value.MaxBodyBytes;

    /// <summary>Writes the Problem Details answer for the exception.</summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="exception">What the endpoint threw.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Always <see langword="true"/>: every exception gets an answer here.</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        ApiError error = ErrorFor(exception);
        string method = httpContext.Request.Method;
        string path = httpContext.Request.Path.Value ?? string.Empty;
        Guid? playerId = HttpCurrentPlayer.Find(httpContext.User);
        if (exception is ConcurrencyConflictException)
        {
            ErrorLog.ConcurrencyConflict(_logger, method, path, playerId);
        }
        else if (exception is CityNotFoundException)
        {
            // The same answer as the token check's own refusal (BearerChallenge); only the log says why.
            AuthLog.TokenRefused(_logger, method, path, _noCityReason);
            httpContext.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
        }
        else if (exception is not BadHttpRequestException)
        {
            ErrorLog.UnhandledException(_logger, exception, method, path, playerId);
        }

        await ApiProblem.Of(error).ExecuteAsync(httpContext);
        return true;
    }

    /// <summary>The catalogue's error for an exception, with a detail that holds the exception's text only in Development.</summary>
    /// <param name="exception">What the endpoint threw.</param>
    /// <returns>The error to answer with.</returns>
    private ApiError ErrorFor(Exception exception) => exception switch
    {
        ConcurrencyConflictException => ErrorCatalogue.ConcurrencyConflict,
        CityNotFoundException => ErrorCatalogue.AuthenticationRequired,
        // No Content-Type at all reaches the binding, which throws; another type is refused earlier, by the routing's own empty 415.
        BadHttpRequestException { StatusCode: StatusCodes.Status415UnsupportedMediaType } => ErrorCatalogue.UnsupportedMediaType,
        BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
            ErrorCatalogue.InvalidArgument.WithDetail($"The body must not be larger than {_maxBodyBytes} bytes."),
        BadHttpRequestException bad => _showExceptionText
            ? ErrorCatalogue.InvalidArgument.WithDetail(bad.Message)
            : ErrorCatalogue.InvalidArgument.WithDetail("The body must be one JSON object with exactly the fields of the command."),
        _ => _showExceptionText
            ? ErrorCatalogue.InternalError.WithDetail($"{exception.GetType().FullName}: {exception.Message}")
            : ErrorCatalogue.InternalError,
    };
}
