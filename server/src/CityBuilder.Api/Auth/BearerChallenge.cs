using CityBuilder.Api.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CityBuilder.Api.Auth;

/// <summary>
/// What the server answers a request that needs an access token and has none that works: 401 with a Problem Details body (RFC 9457) whose
/// <c>code</c> is <c>AUTHENTICATION_REQUIRED</c> (core-instructions/05-api-contract.md, errors; <see cref="ErrorCatalogue"/>). The bearer handler's own challenge sends
/// the status and the <c>WWW-Authenticate</c> header and no body. The answer is the same whether the token is missing, malformed, expired,
/// wrongly signed or from another issuer, and never says which: it only tells the client to log in again.
/// </summary>
internal static class BearerChallenge
{
    /// <summary>
    /// Writes the 401 answer in place of the handler's default one: sets the status and <c>WWW-Authenticate: Bearer</c> (RFC 6750) and
    /// writes the Problem Details body of <see cref="ErrorCatalogue.AuthenticationRequired"/>.
    /// </summary>
    /// <param name="context">The challenge, from the bearer handler's <c>OnChallenge</c> event.</param>
    /// <returns>A task that completes when the answer is written.</returns>
    public static Task WriteProblemAsync(JwtBearerChallengeContext context)
    {
        // The default challenge would run after this event; this one is the whole answer.
        context.HandleResponse();
        context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
        return ApiProblem.Of(ErrorCatalogue.AuthenticationRequired).ExecuteAsync(context.HttpContext);
    }
}
