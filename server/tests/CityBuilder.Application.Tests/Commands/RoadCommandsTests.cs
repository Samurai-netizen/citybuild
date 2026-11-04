using System.Globalization;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.Roads;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// <see cref="PlaceRoadsCommand"/>, <see cref="UpgradeRoadsCommand"/> and <see cref="RemoveRoadsCommand"/> (CR-0008): their shared shape check
/// (1 to 64 tiles, each once, coordinates of at least 0), their pinned canonical payloads, their ledger reasons, that each applies the
/// engine's rule (<see cref="RoadRules"/>) and nothing else, and one run of each through <see cref="CommandPipeline"/> over the in-memory
/// fakes. The rules' checks are the engine's tests; the endpoints and PostgreSQL are the integration tests'.
/// </summary>
public sealed class RoadCommandsTests
{
    /// <summary>The request id of the tests.</summary>
    private static readonly Guid _requestId = new("0199a1b2-c3d4-7e5f-8a9b-00000000000a");

    /// <summary>The three command types, for the theories that hold for each.</summary>
    public static TheoryData<string> CommandTypes => [PlaceRoadsCommand.Type, UpgradeRoadsCommand.Type, RemoveRoadsCommand.Type];

    /// <summary>
    /// The tile lists no city could take are wrong shapes, so they never lock the city (OD-13): none, 65, a null tile, a negative column or
    /// row, a tile twice. One tile, 64 distinct tiles and tiles far outside any grid pass: the grid is the rule's (<c>OUT_OF_BOUNDS</c>,
    /// <c>ROAD_NOT_FOUND</c>).
    /// </summary>
    /// <param name="type">The command type.</param>
    [Theory]
    [MemberData(nameof(CommandTypes))]
    public void ValidateShape_RefusesListsNoCityCouldTake(string type)
    {
        CommandTile?[][] valid =
        [
            [new CommandTile(0, 0)],
            [.. Enumerable.Range(0, 64).Select(index => new CommandTile(index % 8, index / 8))],
            [new CommandTile(int.MaxValue, int.MaxValue), new CommandTile(0, int.MaxValue)],
        ];
        CommandTile?[][] invalid =
        [
            [],
            [.. Enumerable.Range(0, 65).Select(index => new CommandTile(index, 0))],
            [new CommandTile(1, 1), null],
            [new CommandTile(-1, 0)],
            [new CommandTile(0, -1)],
            [new CommandTile(int.MinValue, 3)],
            [new CommandTile(2, 3), new CommandTile(4, 4), new CommandTile(2, 3)],
        ];

        Assert.All(valid, tiles => Assert.Null(Command(type, tiles).ValidateShape()));
        Assert.All(invalid, tiles => Assert.NotNull(Command(type, tiles).ValidateShape()));
    }

    /// <summary>The message names what is wrong: the count, a null tile, or the first tile with a negative coordinate or named twice.</summary>
    /// <param name="tiles">The tiles as <c>x,y</c> pairs separated by <c>;</c>, <c>null</c> for a null tile, empty for none.</param>
    /// <param name="message">The message.</param>
    [Theory]
    [InlineData("", "tiles must list 1 to 64 tiles.")]
    [InlineData("1,1;null", "tiles must hold tile objects { x, y }, not null.")]
    [InlineData("1,1;-2,5;3,-1", "tiles must have coordinates of at least 0: (-2, 5).")]
    [InlineData("1,1;4,0;1,1;4,0", "tiles must name each tile once: (1, 1) is there twice.")]
    public void ValidateShape_SaysWhatIsWrong(string tiles, string message)
    {
        Assert.Equal(message, new PlaceRoadsCommand(_requestId, Parse(tiles)).ValidateShape());
    }

    /// <summary>
    /// Each canonical payload is the command type and the data in ordinal name order, the tiles in the player's order with <c>X</c> before
    /// <c>Y</c>, and its hash is pinned (computed outside the code: <c>printf 'place-roads\n{…}' | shasum -a 256</c>), so a renamed property or
    /// command type, which would turn every stored receipt into a conflict, fails here.
    /// </summary>
    /// <param name="type">The command type.</param>
    /// <param name="hash">The payload's SHA-256, lower-case hex.</param>
    [Theory]
    [InlineData(PlaceRoadsCommand.Type, "dc118f45bb710a8f0b1c915a65079cf5c3bb43d89539f4bbc108fad63ec62a17")]
    [InlineData(UpgradeRoadsCommand.Type, "0a88a5f4aed05460677ee2a50c7f9d54e761d0a64979530d4087e2c6577e2cb4")]
    [InlineData(RemoveRoadsCommand.Type, "821e79eda83d5522fd8cbced86a325c99e6f86cd9fa7f0ede22ccd99786d4237")]
    public void Payload_IsTheCommandTypeAndTheDataWithAPinnedHash(string type, string hash)
    {
        ICityCommand command = Command(type, [new CommandTile(2, 1), new CommandTile(1, 1)]);

        Assert.Equal(
            type + "\n{\"RequestId\":\"0199a1b2-c3d4-7e5f-8a9b-00000000000a\",\"Tiles\":[{\"X\":2,\"Y\":1},{\"X\":1,\"Y\":1}]}",
            CommandPayload.Canonical(command));
        Assert.Equal(hash, Convert.ToHexStringLower(CommandPayload.Hash(command)));
    }

    /// <summary>The same tiles in another order are another payload: a retry must send the body it sent (a reordered one is a conflict).</summary>
    [Fact]
    public void Payload_TilesInAnotherOrder_IsAnotherHash()
    {
        var first = new PlaceRoadsCommand(_requestId, [new CommandTile(2, 1), new CommandTile(1, 1)]);
        var reordered = new PlaceRoadsCommand(_requestId, [new CommandTile(1, 1), new CommandTile(2, 1)]);

        Assert.NotEqual(Convert.ToHexStringLower(CommandPayload.Hash(first)), Convert.ToHexStringLower(CommandPayload.Hash(reordered)));
    }

    /// <summary>Placing and upgrading roads pay <c>COST_ROAD</c>; removing pays nothing; none refunds (CR-0008 D6).</summary>
    [Fact]
    public void LedgerReasons_AreCostRoadForPlaceAndUpgradeAndNoneForRemove()
    {
        CommandTile?[] tiles = [new CommandTile(1, 1)];

        Assert.Equal((LedgerReason.CostRoad, (LedgerReason?)null), (Command(PlaceRoadsCommand.Type, tiles).CostReason, Command(PlaceRoadsCommand.Type, tiles).RefundReason));
        Assert.Equal((LedgerReason.CostRoad, (LedgerReason?)null), (Command(UpgradeRoadsCommand.Type, tiles).CostReason, Command(UpgradeRoadsCommand.Type, tiles).RefundReason));
        Assert.Equal(((LedgerReason?)null, (LedgerReason?)null), (Command(RemoveRoadsCommand.Type, tiles).CostReason, Command(RemoveRoadsCommand.Type, tiles).RefundReason));
    }

    /// <summary>
    /// Apply does exactly what the engine's rule does on a copy of the same city (<see cref="PipelineFixture.TwoSawmillsRepository"/> with a
    /// road at (1, 1); the fixture content has one road level): for each command, free tiles, a road tile, a building's tile and a tile outside
    /// the 4 × 4 grid.
    /// </summary>
    /// <param name="type">The command type.</param>
    /// <param name="tiles">The tiles as <c>x,y</c> pairs separated by <c>;</c>.</param>
    [Theory]
    [InlineData(PlaceRoadsCommand.Type, "2,1;3,1")]
    [InlineData(PlaceRoadsCommand.Type, "1,1")]
    [InlineData(PlaceRoadsCommand.Type, "0,0")]
    [InlineData(PlaceRoadsCommand.Type, "4,0")]
    [InlineData(UpgradeRoadsCommand.Type, "1,1")]
    [InlineData(UpgradeRoadsCommand.Type, "2,1")]
    [InlineData(RemoveRoadsCommand.Type, "1,1")]
    [InlineData(RemoveRoadsCommand.Type, "1,1;2,1")]
    public void Apply_DoesWhatTheEngineRuleDoes(string type, string tiles)
    {
        CityState byCommand = CityWithARoad();
        CityState byRule = CityWithARoad();
        CommandTile?[] parsed = Parse(tiles);
        (int X, int Y)[] engineTiles = [.. parsed.Select(tile => (tile!.X, tile.Y))];

        MutationResult commandResult = Command(type, parsed).Apply(byCommand, SettleFixture.Content);
        MutationResult ruleResult = type switch
        {
            PlaceRoadsCommand.Type => RoadRules.PlaceRoads(byRule, SettleFixture.Content, engineTiles),
            UpgradeRoadsCommand.Type => RoadRules.UpgradeRoads(byRule, SettleFixture.Content, engineTiles),
            _ => RoadRules.RemoveRoads(byRule, SettleFixture.Content, engineTiles),
        };

        Assert.Equal(ruleResult.ToString(), commandResult.ToString());
        Assert.Equal(Describe(byRule), Describe(byCommand));
        Assert.Equal(byRule.Roads, byCommand.Roads);
    }

    /// <summary>
    /// Through the pipeline, placing roads on two free tiles of <see cref="PipelineFixture.TwoSawmillsRepository"/> stores both at level 1,
    /// writes one <c>COST_ROAD</c> row of 10 coins (5 per tile) with the request id as reference, and stores a receipt of type
    /// <c>place-roads</c> with the response.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PlaceRoadsOnTwoFreeTiles_StoresThemPaysAndStoresTheReceipt()
    {
        InMemoryCityRepository repository = PipelineFixture.TwoSawmillsRepository();
        long coinsBefore = repository.Stored(PlayerId).State.Purse;
        var command = new PlaceRoadsCommand(_requestId, [new CommandTile(2, 1), new CommandTile(1, 1)]);

        CommandResult result = await PipelineFixture.Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, command, PipelineFixture.Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        Assert.Equal([new RoadTile(1, 1, 1), new RoadTile(2, 1, 1)], repository.Stored(PlayerId).State.Roads);
        LedgerEntry cost = Assert.Single(repository.LastLedger!, entry => entry.RefType == "command");
        Assert.Equal(
            (LedgerReason.CostRoad, _requestId.ToString(), "coins", -10L, coinsBefore - 10),
            (cost.Reason, cost.RefId, cost.Item.Value, cost.Delta, cost.BalanceAfter));
        CommandReceipt receipt = repository.StoredReceipt(PlayerId, _requestId)!;
        Assert.Equal((PlaceRoadsCommand.Type, result.ResponseJson), (receipt.CommandType, receipt.ResponseJson));
    }

    /// <summary>
    /// Through the pipeline, removing the road of <see cref="CityWithARoad"/> takes it off, writes no ledger row for the command (nothing is
    /// paid or refunded) and stores a receipt of type <c>remove-roads</c>.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RemoveARoad_TakesItOffWithoutLedgerRows()
    {
        InMemoryCityRepository repository = RepositoryWithARoad();
        var command = new RemoveRoadsCommand(_requestId, [new CommandTile(1, 1)]);

        CommandResult result = await PipelineFixture.Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, command, PipelineFixture.Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        Assert.Empty(repository.Stored(PlayerId).State.Roads);
        Assert.DoesNotContain(repository.LastLedger!, entry => entry.RefType == "command");
        Assert.Equal(RemoveRoadsCommand.Type, repository.StoredReceipt(PlayerId, _requestId)!.CommandType);
    }

    /// <summary>The command of a type with the tiles.</summary>
    /// <param name="type">One of the three command types.</param>
    /// <param name="tiles">The tiles.</param>
    /// <returns>The command, with the tests' request id.</returns>
    private static ICityCommand Command(string type, CommandTile?[] tiles) => type switch
    {
        PlaceRoadsCommand.Type => new PlaceRoadsCommand(_requestId, tiles),
        UpgradeRoadsCommand.Type => new UpgradeRoadsCommand(_requestId, tiles),
        RemoveRoadsCommand.Type => new RemoveRoadsCommand(_requestId, tiles),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a road command."),
    };

    /// <summary>Tiles written as <c>x,y</c> pairs separated by <c>;</c>, <c>null</c> for a null tile; the empty text is no tile.</summary>
    /// <param name="tiles">The text.</param>
    /// <returns>The tiles, in order.</returns>
    private static CommandTile?[] Parse(string tiles) => tiles.Length == 0
        ? []
        : [.. tiles.Split(';').Select(tile => tile == "null"
            ? null
            : new CommandTile(
                int.Parse(tile.Split(',')[0], CultureInfo.InvariantCulture), int.Parse(tile.Split(',')[1], CultureInfo.InvariantCulture)))];

    /// <summary>The city of <see cref="PipelineFixture.TwoSawmillsRepository"/> with a level-1 road at (1, 1), placed by the engine's rule.</summary>
    /// <returns>A new copy of that city.</returns>
    private static CityState CityWithARoad() => RepositoryWithARoad().Stored(PlayerId).State;

    /// <summary>
    /// <see cref="PipelineFixture.TwoSawmillsRepository"/> with a level-1 road at (1, 1), placed by the engine's rule on its stored city (the
    /// fake keeps the object, so the next load sees the road).
    /// </summary>
    /// <returns>A new repository.</returns>
    private static InMemoryCityRepository RepositoryWithARoad()
    {
        InMemoryCityRepository repository = PipelineFixture.TwoSawmillsRepository();
        Assert.True(RoadRules.PlaceRoads(repository.Stored(PlayerId).State, SettleFixture.Content, [(1, 1)]).IsOk);
        return repository;
    }
}
