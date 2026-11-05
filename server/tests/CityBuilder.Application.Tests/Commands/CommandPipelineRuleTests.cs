using CityBuilder.Application.Commands;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;
using static CityBuilder.Application.Tests.Commands.PipelineFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// The shape check, the business rules and the settle around them in <see cref="CommandPipeline"/> (<c>03</c>, request lifecycle steps 2, 5
/// to 7; decision 014): a wrong shape never reaches the database, a rejection commits only the settlement, the settlement runs before the
/// mutation, and a refund's ledger rows come before what the closing advance consumes.
/// </summary>
public sealed class CommandPipelineRuleTests
{
    /// <summary>A blank building type is <c>INVALID_ARGUMENT</c> with the command's detail, before any transaction.</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidShape_RefusesBeforeTheDatabase()
    {
        InMemoryCityRepository repository = Repository();

        CommandResult result = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage() with { BuildingTypeId = " " }, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal((CommandOutcome.InvalidArgument, "buildingTypeId must not be blank."), (result.Outcome, result.Detail));
        Assert.Empty(repository.Events);
    }

    /// <summary>An empty request id is <c>INVALID_ARGUMENT</c>, before any transaction: it could never be retried safely.</summary>
    [Fact]
    public async Task ExecuteAsync_EmptyRequestId_RefusesBeforeTheDatabase()
    {
        InMemoryCityRepository repository = Repository();

        CommandResult result = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(Guid.Empty), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.InvalidArgument, result.Outcome);
        Assert.Empty(repository.Events);
    }

    /// <summary>
    /// A rejection an hour later (the tile is taken) commits the settlement: one save with the settlement's rows and no receipt, the last
    /// settled second moved on; the result carries the engine's code and the settlement (an hour), for the settlement log.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RuleRejects_CommitsTheSettlementWithoutAReceipt()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromHours(1));

        CommandResult result = await Pipeline(repository, clock)
            .ExecuteAsync(PlayerId, PlaceCottage() with { X = 0, Y = 0 }, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal((CommandOutcome.Rejected, MutationErrorCode.TileOccupied), (result.Outcome, result.ErrorCode));
        Assert.Equal(3600, result.Settlement?.ElapsedSeconds);
        Assert.Equal(["begin", $"load {PlayerId}", "find receipt", "save", "write", "commit"], repository.Events);
        Assert.Null(repository.LastReceipt);
        Assert.Null(repository.StoredReceipt(PlayerId, RequestId));
        Assert.All(repository.LastLedger!, entry => Assert.Equal("settlement", entry.RefType));
        Assert.Equal(LastSettled.ToUnixTimeSeconds() + 3600, repository.Stored(PlayerId).LastSettledUnixSec);
    }

    /// <summary>A rejection with nothing to settle (no time passed) writes nothing; the transaction commits empty.</summary>
    [Fact]
    public async Task ExecuteAsync_RuleRejectsWithNothingToSettle_WritesNothing()
    {
        InMemoryCityRepository repository = Repository();

        CommandResult result = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage() with { X = 0, Y = 0 }, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Rejected, result.Outcome);
        Assert.Equal(["begin", $"load {PlayerId}", "find receipt", "commit"], repository.Events);
    }

    /// <summary>A rejected request id has no receipt, so sending it again runs the command again instead of replaying.</summary>
    [Fact]
    public async Task ExecuteAsync_RetryOfARejectedCommand_RunsAgain()
    {
        InMemoryCityRepository repository = Repository();
        PlaceTestCommand occupied = PlaceCottage() with { X = 0, Y = 0 };
        await Pipeline(repository, ClockAtLastSettled()).ExecuteAsync(PlayerId, occupied, Writer(repository), TestContext.Current.CancellationToken);

        CommandResult again = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, occupied, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal((CommandOutcome.Rejected, MutationErrorCode.TileOccupied), (again.Outcome, again.ErrorCode));
    }

    /// <summary>
    /// The settle runs before the mutation: with all three builders busy (the manor and two new cottages) a placement is
    /// <c>NO_FREE_BUILDER</c>, but a minute later the cottages' constructions complete in the settle and free their builders for the command.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ConstructionCompletedWhileAway_FreesItsBuilderForTheCommand()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        await Pipeline(repository, clock).ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);
        await Pipeline(repository, clock)
            .ExecuteAsync(PlayerId, new PlaceTestCommand(Guid.CreateVersion7(), "cottage", 0, 2), Writer(repository), TestContext.Current.CancellationToken);
        PlaceTestCommand next = new(Guid.CreateVersion7(), "cottage", 2, 1);
        CommandResult busy = await Pipeline(repository, clock).ExecuteAsync(PlayerId, next, Writer(repository), TestContext.Current.CancellationToken);
        Assert.Equal(MutationErrorCode.NoFreeBuilder, busy.ErrorCode);
        clock.Advance(TimeSpan.FromSeconds(StartClock));

        CommandResult result = await Pipeline(repository, clock).ExecuteAsync(PlayerId, next, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
    }

    /// <summary>
    /// A minute after <see cref="PipelineFixture.TwoSawmillsRepository"/> was settled, upgrading the producing sawmill 3 ledgers in the order
    /// things happened, each balance the city's total: the settlement's log (0 → 1, the woodcutter's cycle at 240), then the command's refund
    /// of the cycle's 2 logs into sawmill 3's own store (1 → 3) and its 10 coins (50 → 40). The closing advance starts nothing: sawmill 3 is
    /// upgrading, and sawmill 5's store has no logs (a refund never reaches another store, CR-0008).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RefundAfterTheSettlement_LedgersInTheOrderItHappened()
    {
        InMemoryCityRepository repository = TwoSawmillsRepository();
        CityState before = repository.Stored(PlayerId).State;
        Assert.Equal((BuildingPhase.Producing, 0L), (before.Buildings.Single(b => b.Id == 3).Phase, Total(before, Logs)));
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(CycleSeconds));

        CommandResult result = await Pipeline(repository, clock)
            .ExecuteAsync(PlayerId, new UpgradeTestCommand(RequestId, BuildingId: 3), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        Assert.Equal(
            [
                (Logs.Value, 1L, 1L, "SettlementProduced", "settlement"),
                (Logs.Value, 2L, 3L, "RefundUpgrade", "command"),
                ("coins", -10L, 40L, "CostUpgrade", "command"),
            ],
            repository.LastLedger!.Select(entry => (entry.Item.Value, entry.Delta, entry.BalanceAfter, entry.Reason.ToString(), entry.RefType)));
        CityState after = repository.Stored(PlayerId).State;
        Assert.Equal((2L, BuildingPhase.Idle), (after.Buildings.Single(b => b.Id == 3).GetStock(Logs), after.Buildings.Single(b => b.Id == 5).Phase));
    }
}
