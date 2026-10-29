using System.Net;
using System.Reflection;
using CityBuilder.Api.Errors;
using CityBuilder.Simulation.Rules;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// The error catalogue (<see cref="ErrorCatalogue"/>) against the error table of <c>core-instructions/05-api-contract.md</c>, written out
/// here from the table: every code has its status, its <c>type</c> and a title and detail; the answer a client reads has exactly the
/// contract's members for every code, with and without a request id; and a code can't be in the catalogue without a row here and a source
/// that produces it (<see cref="CommandErrorMappingTests"/> holds the engine's codes, a table below the others). In memory,
/// no Docker.
/// </summary>
/// <param name="factory">The in-memory server.</param>
public sealed class ErrorCatalogueTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The table of <c>05</c>: every code with its HTTP status.</summary>
    public static TheoryData<string, int> Table => new()
    {
        { "AUTHENTICATION_REQUIRED", 401 },
        { "INVALID_ARGUMENT", 400 },
        { "UNKNOWN_BUILDING_TYPE", 422 },
        { "BUILDING_LOCKED", 422 },
        { "OUT_OF_BOUNDS", 422 },
        { "TILE_OCCUPIED", 409 },
        { "INSUFFICIENT_RESOURCE", 422 },
        { "INSUFFICIENT_WORKERS", 422 },
        { "WORKER_LIMIT", 422 },
        { "NO_FREE_BUILDER", 409 },
        { "BUILDING_NOT_FOUND", 404 },
        { "BUILDING_BUSY", 409 },
        { "MAX_LEVEL", 422 },
        { "UNKNOWN_RESEARCH", 422 },
        { "RESEARCH_LOCKED", 422 },
        { "RESEARCH_IN_PROGRESS", 409 },
        { "RESEARCH_COMPLETED", 409 },
        { "IDEMPOTENCY_CONFLICT", 409 },
        { "RATE_LIMITED", 429 },
        { "CONCURRENCY_CONFLICT", 409 },
        { "INTERNAL_ERROR", 500 },
        { "NOT_FOUND", 404 },
        { "METHOD_NOT_ALLOWED", 405 },
        { "UNSUPPORTED_MEDIA_TYPE", 415 },
    };

    /// <summary>
    /// The codes no engine rule decides, each with the test class that proves a request produces it over HTTP: the token check, the shape and
    /// request-id checks, the pipeline, the rate limiter, the exception handler and the status code pages.
    /// </summary>
    private static readonly Dictionary<string, string> _nonEngineCodes = new(StringComparer.Ordinal)
    {
        ["AUTHENTICATION_REQUIRED"] = nameof(UnauthenticatedContractTests),
        ["INVALID_ARGUMENT"] = nameof(PlaceBuildingContractTests),
        ["IDEMPOTENCY_CONFLICT"] = nameof(PlaceBuildingContractTests),
        ["RATE_LIMITED"] = "RateLimitTests",
        ["CONCURRENCY_CONFLICT"] = "ExceptionHandlerTests",
        ["INTERNAL_ERROR"] = "ExceptionHandlerTests",
        ["NOT_FOUND"] = "ExceptionHandlerTests",
        ["METHOD_NOT_ALLOWED"] = "ExceptionHandlerTests",
        ["UNSUPPORTED_MEDIA_TYPE"] = "ExceptionHandlerTests",
    };

    /// <summary>A request id for the answers that carry one.</summary>
    private static readonly Guid _requestId = Guid.Parse("6f1c0b9a-3d2e-4f51-8a77-0c9d5e1b2a34");

    /// <summary>Each code of the table has its status, the <c>type</c> made from the code, a title and a detail.</summary>
    /// <param name="code">The code.</param>
    /// <param name="status">The HTTP status of the table.</param>
    [Theory]
    [MemberData(nameof(Table))]
    public void Code_HasTheTablesStatusAndItsTexts(string code, int status)
    {
        ApiError error = Assert.Single(ErrorCatalogue.All, candidate => candidate.Code == code);

        Assert.Equal(status, error.Status);
        Assert.Equal("urn:citybuilder:error:" + code.Replace('_', '-').ToLowerInvariant(), error.Type);
        Assert.False(string.IsNullOrWhiteSpace(error.Title));
        Assert.False(string.IsNullOrWhiteSpace(error.Detail));
    }

    /// <summary>The catalogue holds exactly the codes of the table, once each, so a new code fails here until it has its row.</summary>
    [Fact]
    public void All_IsExactlyTheTable()
    {
        string[] inTable = Table.Select(row => (string)row.Data.Item1).ToArray();

        Assert.Equal(inTable, ErrorCatalogue.All.Select(error => error.Code));
        Assert.Equal(inTable.Length, inTable.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Every <see cref="ApiError"/> member of the catalogue is in <see cref="ErrorCatalogue.All"/>: none is written and forgotten.</summary>
    [Fact]
    public void All_HoldsEveryMemberOfTheCatalogue()
    {
        ApiError[] members = typeof(ErrorCatalogue)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ApiError))
            .Select(field => (ApiError)field.GetValue(null)!)
            .ToArray();

        Assert.Equal(members.Select(error => error.Code).Order(StringComparer.Ordinal), ErrorCatalogue.All.Select(error => error.Code).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Every code has a source: the engine's mapping or one of the non-engine codes (<c>INVALID_ARGUMENT</c> is both: the engine refuses
    /// workers on a house with it, and the shape checks use it). A code that exists in the catalogue and that nothing produces fails here,
    /// and so does a non-engine entry for a code the catalogue doesn't have.
    /// </summary>
    [Fact]
    public void EveryCode_HasASourceThatProducesIt()
    {
        string[] engine = Enum.GetValues<MutationErrorCode>().Select(code => ErrorCatalogue.Of(code).Code).ToArray();

        Assert.Equal(
            ErrorCatalogue.All.Select(error => error.Code).Order(StringComparer.Ordinal),
            engine.Concat(_nonEngineCodes.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// What a client reads for each code has exactly the contract's members: the status, the code and its type, a title and a detail, a trace id,
    /// and the request id only when the request had one. Without one the answer has no <c>requestId</c> property at all.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="status">The HTTP status of the table.</param>
    [Theory]
    [MemberData(nameof(Table))]
    public async Task Answer_HasExactlyTheContractsMembers(string code, int status)
    {
        using var server = ErrorEndpoint.AddTo(factory);

        ApiResponse without = await ApiClient.GetAsync(server, "/test/errors/" + code, playerId: null);
        ApiResponse with = await ApiClient.GetAsync(server, $"/test/errors/{code}?requestId={_requestId}", playerId: null);

        ProblemAssert.AssertProblem(without, (HttpStatusCode)status, code, requestId: null);
        ProblemAssert.AssertProblem(with, (HttpStatusCode)status, code, _requestId.ToString());
    }
}
