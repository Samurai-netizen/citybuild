using System.Net;
using System.Text.Json.Nodes;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// The Problem Details check the command contract tests share (<c>core-instructions/05-api-contract.md</c> → Errors), so every command's
/// refusals are held to the same shape.
/// </summary>
internal static class ProblemAssert
{
    /// <summary>
    /// Asserts a Problem Details answer with exactly the contract's members: the status, the code and its <c>type</c>, a title and a detail,
    /// the request id (only when the request had one) and a trace id.
    /// </summary>
    /// <param name="response">The answer.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    /// <param name="requestId">The expected request id, or <see langword="null"/> when the answer has none.</param>
    public static void AssertProblem(ApiResponse response, HttpStatusCode status, string code, string? requestId)
    {
        Assert.Equal(status, response.Status);
        Assert.Equal("application/problem+json", response.ContentType);
        JsonNode problem = response.Json();
        JsonShape.AssertObject(
            problem,
            "type:string title:string status:int detail:string code:string " + (requestId is null ? string.Empty : "requestId:string ") + "traceId:string",
            "$");
        Assert.Equal((int)status, (int)problem["status"]!);
        Assert.Equal(code, (string)problem["code"]!);
        Assert.Equal("urn:citybuilder:error:" + code.Replace('_', '-').ToLowerInvariant(), (string)problem["type"]!);
        if (requestId is not null)
        {
            Assert.Equal(requestId, (string)problem["requestId"]!);
        }
    }
}
