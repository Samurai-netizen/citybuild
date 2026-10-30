using CityBuilder.Application.Commands;
using CityBuilder.Application.Persistence;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;
using static CityBuilder.Application.Tests.Commands.PipelineFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// A command that succeeds through <see cref="CommandPipeline"/> (<c>03</c>, request lifecycle; <c>06</c>, write transaction pattern): the
/// order of the steps, the receipt, the response writer's input, and the ledger rows of the settlement and the cost. Over
/// <see cref="SettleFixture.Repository"/>'s city (60 coins; cottage, woodcutter, manor constructing; one builder free).
/// </summary>
public sealed class CommandPipelineTests
{
    /// <summary>
    /// Lock and load, the receipt lookup, the response, one save of the city, its ledger and receipt, the write and the commit, in that order.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NewCommand_RunsTheStepsInOrder()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();

        CommandResult result = await Pipeline(repository, clock)
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        Assert.Equal(["begin", $"load {PlayerId}", "find receipt", "respond", "save", "write", "commit"], repository.Events);
    }

    /// <summary>The receipt holds the request id, the command type, the payload's hash and the response, which is also the result's.</summary>
    [Fact]
    public async Task ExecuteAsync_NewCommand_StoresTheReceiptWithTheResponse()
    {
        InMemoryCityRepository repository = Repository();
        PlaceTestCommand command = PlaceCottage();

        CommandResult result = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, command, Writer(repository), TestContext.Current.CancellationToken);

        CommandReceipt receipt = Assert.IsType<CommandReceipt>(repository.StoredReceipt(PlayerId, RequestId));
        Assert.Equal((PlaceTestCommand.Type, result.ResponseJson), (receipt.CommandType, receipt.ResponseJson));
        Assert.Equal(CommandPayload.Hash(command), receipt.PayloadHash.ToArray());
    }

    /// <summary>
    /// The writer gets the city as saved, after the settle (a minute: clock 60 → 120), the mutation (a fourth building) and the closing
    /// advance, with the next state version (0 → 1) and the server second. The result carries that settlement (a minute), for the log.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NewCommand_GivesTheWriterTheSavedCity()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromMinutes(1));

        CommandResult result = await Pipeline(repository, clock)
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        long serverSecond = LastSettled.ToUnixTimeSeconds() + 60;
        Assert.Equal(
            $"{{\"requestId\":\"{RequestId}\",\"stateVersion\":1,\"clock\":120,\"serverTime\":{serverSecond},\"buildings\":4}}",
            result.ResponseJson);
        Assert.Equal(60, result.Settlement?.ElapsedSeconds);
        Building placed = repository.Stored(PlayerId).State.Buildings.Single(building => building.Id == 4);
        Assert.Equal((new BuildingTypeId("cottage"), 1, 1, BuildingPhase.Constructing), (placed.TypeId, placed.X, placed.Y, placed.Phase));
    }

    /// <summary>
    /// After an hour the settlement's rows come first (the woodcutter's 60 logs, with the settlement's reference), then the cost, as
    /// <c>COST_PLACE</c> with the command's reference: 60 coins → 50.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PaidCommand_LedgersTheCostAfterTheSettlement()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromHours(1));

        await Pipeline(repository, clock).ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                (Logs.Value, 60L, 60L, "SettlementProduced", "settlement"),
                ("coins", -10L, 50L, "CostPlace", "command"),
            ],
            repository.LastLedger!.Select(entry => (entry.Item.Value, entry.Delta, entry.BalanceAfter, entry.Reason.ToString(), entry.RefType)));
        Assert.Equal(RequestId.ToString(), repository.LastLedger![^1].RefId);
    }

    /// <summary>
    /// A concurrency conflict on the save (a lock timeout, a stale version) propagates, with nothing committed and no receipt: the client
    /// retries with the same request id (<c>05</c>, <c>CONCURRENCY_CONFLICT</c>).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ConcurrencyConflictOnTheSave_PropagatesAndCommitsNothing()
    {
        InMemoryCityRepository repository = Repository();
        repository.FailNextWrite = new ConcurrencyConflictException();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken));

        Assert.Equal("rollback", repository.Events[^1]);
        Assert.Null(repository.StoredReceipt(PlayerId, RequestId));
    }
}
