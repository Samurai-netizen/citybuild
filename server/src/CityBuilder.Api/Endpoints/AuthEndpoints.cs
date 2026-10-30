using CityBuilder.Api.Auth;
using CityBuilder.Api.Contracts;
using CityBuilder.Api.Errors;
using CityBuilder.Api.Http;
using CityBuilder.Application.Players;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CityBuilder.Api.Endpoints;

/// <summary>The authentication endpoints under <c>/api/v1/auth</c> (core-instructions/05-api-contract.md).</summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps <c>POST /auth/dev-login</c> on the <c>/api/v1</c> group, open to requests without a token. The caller maps it only in Development
    /// with <c>Auth:DevLoginEnabled</c> on; anywhere else the path answers 404.
    /// </summary>
    /// <param name="api">The <c>/api/v1</c> route group.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapDevLogin(this RouteGroupBuilder api) =>
        api.MapPost("/auth/dev-login", DevLoginAsync).AllowAnonymous().RequireRateLimit(RateLimitClass.DevLogin);

    /// <summary>
    /// Logs the display name in (<see cref="DevLogin"/>: on the first login the player and the starter city are created) and answers with
    /// an access token for the player. A missing body or a name that isn't allowed is <c>INVALID_ARGUMENT</c> (400). Both are logged
    /// (<see cref="AuthLog"/>) with the player or the code, never the name or the token.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="devLogin">The use case.</param>
    /// <param name="tokens">Issues the token.</param>
    /// <param name="loggers">Gives the logger of this class, where the login is logged.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>200 with the token, or 400 with ProblemDetails.</returns>
    private static async Task<Results<Ok<DevLoginResponse>, ProblemHttpResult>> DevLoginAsync(
        DevLoginRequest? request, DevLogin devLogin, AccessTokens tokens, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        ILogger logger = loggers.CreateLogger(typeof(AuthEndpoints).FullName!);
        if (request is null || !DevLogin.IsValidDisplayName(request.DisplayName))
        {
            AuthLog.DevLoginRefused(logger, ErrorCatalogue.InvalidArgument.Code);
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail(
                $"displayName must be 1 to {DevLogin.MaxDisplayNameLength} ASCII letters, digits, '_', '-' or '.'."));
        }

        DevLoginResult result = await devLogin.ExecuteAsync(request.DisplayName, cancellationToken);
        AccessToken token = tokens.Issue(result.PlayerId);
        AuthLog.DevLoginSucceeded(logger, result.PlayerId, result.IsNewPlayer);
        return TypedResults.Ok(new DevLoginResponse(token.Token, token.ExpiresAt));
    }
}
