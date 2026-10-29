using CityBuilder.Api.Errors;
using CityBuilder.Simulation.Rules;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// The answer of every engine rejection (<see cref="ErrorCatalogue.Of"/>) against the error table of
/// <c>core-instructions/05-api-contract.md</c>: each engine code has its API code and HTTP status, written out here from the table, and no
/// code is left unmapped or mapped twice. The endpoints that reach each code over HTTP test it there (PlaceBuilding's on PostgreSQL); <see cref="ErrorCatalogueTests"/> holds every
/// code of the catalogue, the engine's and the others, to the table and the shape.
/// </summary>
public sealed class CommandErrorMappingTests
{
    /// <summary>The engine's codes with the code and status <c>05</c> gives them.</summary>
    public static TheoryData<MutationErrorCode, string, int> Table => new()
    {
        { MutationErrorCode.InvalidArgument, "INVALID_ARGUMENT", 400 },
        { MutationErrorCode.UnknownBuildingType, "UNKNOWN_BUILDING_TYPE", 422 },
        { MutationErrorCode.BuildingLocked, "BUILDING_LOCKED", 422 },
        { MutationErrorCode.OutOfBounds, "OUT_OF_BOUNDS", 422 },
        { MutationErrorCode.TileOccupied, "TILE_OCCUPIED", 409 },
        { MutationErrorCode.InsufficientResource, "INSUFFICIENT_RESOURCE", 422 },
        { MutationErrorCode.InsufficientWorkers, "INSUFFICIENT_WORKERS", 422 },
        { MutationErrorCode.WorkerLimit, "WORKER_LIMIT", 422 },
        { MutationErrorCode.NoFreeBuilder, "NO_FREE_BUILDER", 409 },
        { MutationErrorCode.BuildingNotFound, "BUILDING_NOT_FOUND", 404 },
        { MutationErrorCode.BuildingBusy, "BUILDING_BUSY", 409 },
        { MutationErrorCode.MaxLevel, "MAX_LEVEL", 422 },
        { MutationErrorCode.UnknownResearch, "UNKNOWN_RESEARCH", 422 },
        { MutationErrorCode.ResearchLocked, "RESEARCH_LOCKED", 422 },
        { MutationErrorCode.ResearchInProgress, "RESEARCH_IN_PROGRESS", 409 },
        { MutationErrorCode.ResearchCompleted, "RESEARCH_COMPLETED", 409 },
    };

    /// <summary>Each engine code answers with the API code and status of the contract's table, and the type made from the code.</summary>
    /// <param name="engineCode">The engine's code.</param>
    /// <param name="code">The API code.</param>
    /// <param name="status">The HTTP status.</param>
    [Theory]
    [MemberData(nameof(Table))]
    public void Of_EngineCode_IsTheContractsCodeAndStatus(MutationErrorCode engineCode, string code, int status)
    {
        ApiError error = ErrorCatalogue.Of(engineCode);

        Assert.Equal((code, status), (error.Code, error.Status));
        Assert.Equal("urn:citybuilder:error:" + code.Replace('_', '-').ToLowerInvariant(), error.Type);
        Assert.False(string.IsNullOrWhiteSpace(error.Title));
        Assert.False(string.IsNullOrWhiteSpace(error.Detail));
    }

    /// <summary>The table above names every engine code once, so a new engine code fails here until it has its row and its mapping.</summary>
    [Fact]
    public void Table_CoversEveryEngineCodeOnce()
    {
        MutationErrorCode[] inTable = Table.Select(row => row.Data.Item1).ToArray();

        Assert.Equal(Enum.GetValues<MutationErrorCode>().Order(), inTable.Order());
        Assert.Equal(inTable.Length, inTable.Select(row => ErrorCatalogue.Of(row).Code).Distinct(StringComparer.Ordinal).Count());
    }
}
