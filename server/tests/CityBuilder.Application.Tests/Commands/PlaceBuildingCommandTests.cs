using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.PlaceBuilding;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// <see cref="PlaceBuildingCommand"/>: its shape check, its pinned canonical payload, its ledger reasons, that it applies the engine's rule
/// (<see cref="CityRules.PlaceBuilding"/>) and nothing else, and one run through <see cref="CommandPipeline"/> over the in-memory fakes. The
/// rule's checks are the engine's tests; the endpoint and PostgreSQL are the integration tests'.
/// </summary>
public sealed class PlaceBuildingCommandTests
{
    /// <summary>The request id of the tests.</summary>
    private static readonly Guid _requestId = new("0199a1b2-c3d4-7e5f-8a9b-00000000000a");

    /// <summary>
    /// A type id that can't be a content id is a wrong shape (missing, blank, too long, a character the id format doesn't have), so it
    /// never locks the city; an id of the right form is left to the content, even one the content doesn't have.
    /// </summary>
    /// <param name="typeId">The type id.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("\t", false)]
    [InlineData("woodcutter", true)]
    [InlineData("no_such_building", true)]
    [InlineData("no-such-building", false)]
    [InlineData("Woodcutter", false)]
    [InlineData("woodcutter ", false)]
    [InlineData("1woodcutter", false)]
    [InlineData("w\u00f6odcutter", false)]
    public void ValidateShape_TypeId_RefusesWhatCantBeAContentId(string typeId, bool valid)
    {
        string? error = new PlaceBuildingCommand(_requestId, typeId, 2, 3).ValidateShape();

        Assert.Equal(valid, error is null);
    }

    /// <summary>
    /// The id format allows 64 characters: an id of 64 passes the shape check, one of 65 (or a megabyte of letters) is refused before the
    /// city is locked.
    /// </summary>
    /// <param name="length">The id's length.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    [InlineData(1_000_000, false)]
    public void ValidateShape_TypeIdLength_AllowsTheIdFormatsSixtyFour(int length, bool valid)
    {
        string? error = new PlaceBuildingCommand(_requestId, new string('a', length), 2, 3).ValidateShape();

        Assert.Equal(valid, error is null);
    }

    /// <summary>
    /// A negative coordinate is a wrong shape (the grid starts at 0, whatever the content), so it never locks the city (OD-13); a coordinate
    /// past the grid passes, because the content sets the grid's size, and the rule answers <c>OUT_OF_BOUNDS</c>.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(8, 8, true)]
    [InlineData(int.MaxValue, int.MaxValue, true)]
    [InlineData(-1, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(int.MinValue, int.MaxValue, false)]
    public void ValidateShape_Coordinates_RefusesNegativeOnes(int x, int y, bool valid)
    {
        string? error = new PlaceBuildingCommand(_requestId, "woodcutter", x, y).ValidateShape();

        Assert.Equal(valid, error is null);
    }

    /// <summary>
    /// The canonical payload is <c>place-building</c> and the data in ordinal name order, and its hash is pinned (computed outside the code:
    /// <c>printf 'place-building\n{…}' | shasum -a 256</c>), so a renamed property or command type, which would turn every stored receipt
    /// into a conflict, fails here.
    /// </summary>
    [Fact]
    public void Payload_IsTheCommandTypeAndTheDataWithAPinnedHash()
    {
        var command = new PlaceBuildingCommand(_requestId, "woodcutter", 2, 3);

        Assert.Equal(
            "place-building\n{\"BuildingTypeId\":\"woodcutter\",\"RequestId\":\"0199a1b2-c3d4-7e5f-8a9b-00000000000a\",\"X\":2,\"Y\":3}",
            CommandPayload.Canonical(command));
        Assert.Equal("151035b7bf09b19007c9c5e981cecbb6c4645db009f6533870d5882c21480285", Convert.ToHexStringLower(CommandPayload.Hash(command)));
    }

    /// <summary>Placing pays <c>COST_PLACE</c> and never refunds.</summary>
    [Fact]
    public void LedgerReasons_AreCostPlaceAndNoRefund()
    {
        var command = new PlaceBuildingCommand(_requestId, "woodcutter", 2, 3);

        Assert.Equal((LedgerReason.CostPlace, (LedgerReason?)null), (command.CostReason, command.RefundReason));
    }

    /// <summary>Apply does exactly what the engine's rule does on a copy of the same city, for a placement and for a refusal.</summary>
    /// <param name="typeId">The type to place.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    [Theory]
    [InlineData("woodcutter", 2, 3)]
    [InlineData("castle", 2, 3)]
    [InlineData("woodcutter", -1, 3)]
    public void Apply_DoesWhatTheEngineRuleDoes(string typeId, int x, int y)
    {
        CityState byCommand = CityFactory.CreateStarterCity(SettleFixture.Content);
        CityState byRule = CityFactory.CreateStarterCity(SettleFixture.Content);

        MutationResult commandResult = new PlaceBuildingCommand(_requestId, typeId, x, y).Apply(byCommand, SettleFixture.Content);
        MutationResult ruleResult = CityRules.PlaceBuilding(byRule, SettleFixture.Content, new BuildingTypeId(typeId), x, y);

        Assert.Equal(ruleResult.ToString(), commandResult.ToString());
        Assert.Equal(ruleResult.Spent, commandResult.Spent);
        Assert.Equal(Describe(byRule), Describe(byCommand));
    }

    /// <summary>
    /// Through the pipeline, the command places the building, writes its cost under <c>COST_PLACE</c> with the request id as reference, and
    /// stores a receipt of type <c>place-building</c> with the response.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PlaceWoodcutter_SavesTheBuildingTheCostAndTheReceipt()
    {
        InMemoryCityRepository repository = RepositoryWith(CityFactory.CreateStarterCity(SettleFixture.Content));
        var command = new PlaceBuildingCommand(_requestId, "woodcutter", 2, 3);

        CommandResult result = await PipelineFixture.Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, command, PipelineFixture.Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        CityAggregate stored = repository.Stored(PlayerId);
        Building building = Assert.Single(stored.State.Buildings);
        Assert.Equal(("woodcutter", 2, 3, BuildingPhase.Constructing), (building.TypeId.Value, building.X, building.Y, building.Phase));
        LedgerEntry cost = Assert.Single(repository.LastLedger!);
        Assert.Equal((LedgerReason.CostPlace, _requestId.ToString()), (cost.Reason, cost.RefId));
        Assert.True(cost.Delta < 0);
        CommandReceipt receipt = repository.StoredReceipt(PlayerId, _requestId)!;
        Assert.Equal((PlaceBuildingCommand.Type, result.ResponseJson), (receipt.CommandType, receipt.ResponseJson));
    }
}
