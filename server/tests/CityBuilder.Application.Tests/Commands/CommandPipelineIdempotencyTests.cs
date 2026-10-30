using CityBuilder.Application.Commands;
using CityBuilder.Application.Persistence;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;
using static CityBuilder.Application.Tests.Commands.PipelineFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// The idempotency of <see cref="CommandPipeline"/> (<c>05</c>, idempotency rules; ADR-0004; <c>06</c>, the receipt race): a retry returns the
/// stored response without running, another payload under the same request id is a conflict, another player's request id never matches,
/// and an identical request that committed first answers through its receipt.
/// </summary>
public sealed class CommandPipelineIdempotencyTests
{
    /// <summary>
    /// The same request an hour later returns the stored text and runs nothing: no response is written, nothing is saved, and the city isn't
    /// even settled (its last settled second stays).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SameRequestAgain_ReturnsTheStoredResponse()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        CommandResult first = await Pipeline(repository, clock)
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);
        repository.Events.Clear();
        clock.Advance(TimeSpan.FromHours(1));

        CommandResult again = await Pipeline(repository, clock)
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal((CommandOutcome.Replayed, first.ResponseJson), (again.Outcome, again.ResponseJson));
        Assert.Null(again.Settlement);
        Assert.Equal(["begin", $"load {PlayerId}", "find receipt", "commit"], repository.Events);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(LastSettled.ToUnixTimeSeconds(), repository.Stored(PlayerId).LastSettledUnixSec);
    }

    /// <summary>The same request id with another tile is <c>IDEMPOTENCY_CONFLICT</c>: nothing runs, nothing is saved.</summary>
    [Fact]
    public async Task ExecuteAsync_SameRequestIdOtherPayload_IsAConflict()
    {
        InMemoryCityRepository repository = Repository();
        await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        CommandResult conflict = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage() with { X = 3, Y = 3 }, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.IdempotencyConflict, conflict.Outcome);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(4, repository.Stored(PlayerId).State.Buildings.Count);
    }

    /// <summary>The same request id on another command is a conflict too: the command type is part of the hash.</summary>
    [Fact]
    public async Task ExecuteAsync_SameRequestIdOtherCommand_IsAConflict()
    {
        InMemoryCityRepository repository = Repository();
        await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        CommandResult conflict = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, new UpgradeTestCommand(RequestId, BuildingId: 2), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.IdempotencyConflict, conflict.Outcome);
    }

    /// <summary>Another player sending the same request id runs their own command: the receipt's key includes the player.</summary>
    [Fact]
    public async Task ExecuteAsync_AnotherPlayersRequestId_RunsAsNew()
    {
        InMemoryCityRepository repository = WithSecondPlayer(Repository());
        await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        CommandResult other = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(OtherPlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, other.Outcome);
        Assert.Single(repository.Stored(OtherPlayerId).State.Buildings);
        Assert.NotNull(repository.StoredReceipt(OtherPlayerId, RequestId));
    }

    /// <summary>
    /// An identical request commits its receipt between this one's lookup and its write: the unique key refuses the insert, the transaction
    /// rolls back, and the stored receipt answers with its response.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_IdenticalRequestCommittedFirst_ReturnsItsResponse()
    {
        InMemoryCityRepository repository = Repository();
        repository.BeforeNextWrite = () => repository.AddReceipt(
            PlayerId, new CommandReceipt(RequestId, PlaceTestCommand.Type, CommandPayload.Hash(PlaceCottage()), "{\"winner\":true}"));

        CommandResult result = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal((CommandOutcome.Replayed, "{\"winner\":true}"), (result.Outcome, result.ResponseJson));
        Assert.Equal(["begin", $"load {PlayerId}", "find receipt", "respond", "save", "rollback", "find receipt"], repository.Events);
    }

    /// <summary>A request with the same id but another payload that committed first makes the losing insert a conflict.</summary>
    [Fact]
    public async Task ExecuteAsync_OtherPayloadCommittedFirst_IsAConflict()
    {
        InMemoryCityRepository repository = Repository();
        repository.BeforeNextWrite = () => repository.AddReceipt(
            PlayerId, new CommandReceipt(RequestId, PlaceTestCommand.Type, new byte[CommandReceipt.PayloadHashLength], "{\"winner\":true}"));

        CommandResult result = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.IdempotencyConflict, result.Outcome);
    }

    /// <summary>A duplicate key without a receipt behind it is no receipt race: the exception propagates and nothing is committed.</summary>
    [Fact]
    public async Task ExecuteAsync_DuplicateKeyWithoutAReceipt_Throws()
    {
        InMemoryCityRepository repository = Repository();
        repository.FailNextWrite = new DuplicateKeyException("ux_city_buildings_city_id_x_y");

        await Assert.ThrowsAsync<DuplicateKeyException>(() => Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken));

        Assert.DoesNotContain("commit", repository.Events);
    }
}
