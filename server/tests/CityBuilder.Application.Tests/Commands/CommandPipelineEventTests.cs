using CityBuilder.Application.Commands;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Metrics;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;
using static CityBuilder.Application.Tests.Commands.PipelineFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// The command's player event (CR-0009, <c>command.&lt;type&gt;</c>) in <see cref="CommandPipeline"/>: a command applied or refused by a game
/// rule records exactly one event in its transaction, with the command's detail and the outcome (<c>accepted</c> or the error code), after
/// <c>session.start</c> when the command starts a session; a
/// replay, an idempotency conflict, a shape refusal, the receipt race and a failed write record none.
/// </summary>
public sealed class CommandPipelineEventTests
{
    /// <summary>An applied command records one event: its type, its detail, <c>accepted</c>, the settled clock and the server time.</summary>
    [Fact]
    public async Task ExecuteAsync_Accepted_RecordsOneEvent()
    {
        InMemoryCityRepository repository = Repository();

        await Pipeline(repository, ClockAtLastSettled()).ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        PlayerEvent recorded = Assert.Single(repository.PlayerEvents);
        Assert.Equal(
            (PlayerId, "command.place-test", "cottage", "accepted", (long?)null, repository.Stored(PlayerId).State.Clock, LastSettled),
            (recorded.PlayerId, recorded.EventType, recorded.Detail, recorded.Outcome, recorded.Value, recorded.CityClock, recorded.OccurredAt));
        Assert.Equal(1, repository.SaveChangesCount);
    }

    /// <summary>
    /// A rule's refusal within the session gap records one event with the error code as the outcome, written with the settlement it commits
    /// (one write); an unknown type gives no detail.
    /// </summary>
    /// <param name="typeId">The type to place.</param>
    /// <param name="x">The tile's column (0 is taken by the cottage at row 0).</param>
    /// <param name="detail">The event's detail.</param>
    /// <param name="outcome">The event's outcome.</param>
    [Theory]
    [InlineData("cottage", 0, "cottage", "TILE_OCCUPIED")]
    [InlineData("castle", 1, null, "UNKNOWN_BUILDING_TYPE")]
    public async Task ExecuteAsync_RuleRejects_RecordsOneEventWithTheCode(string typeId, int x, string? detail, string outcome)
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromMinutes(20));

        await Pipeline(repository, clock).ExecuteAsync(
            PlayerId, PlaceCottage() with { BuildingTypeId = typeId, X = x, Y = 0 }, Writer(repository), TestContext.Current.CancellationToken);

        PlayerEvent recorded = Assert.Single(repository.PlayerEvents);
        Assert.Equal(("command.place-test", detail, outcome), (recorded.EventType, recorded.Detail, recorded.Outcome));
        Assert.Equal((1, 1), (repository.SaveCount, repository.SaveChangesCount));
    }

    /// <summary>
    /// A refusal at an instant with nothing to settle still records its event: the runner writes only the event before the commit, and the
    /// city is not saved (its state version stays).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RuleRejectsWithNothingToSettle_RecordsOnlyTheEvent()
    {
        InMemoryCityRepository repository = Repository();

        await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage() with { X = 0, Y = 0 }, Writer(repository), TestContext.Current.CancellationToken);

        PlayerEvent recorded = Assert.Single(repository.PlayerEvents);
        Assert.Equal("TILE_OCCUPIED", recorded.Outcome);
        Assert.Equal(0, repository.SaveCount);
        Assert.Equal(0, repository.Stored(PlayerId).StateVersion);
    }

    /// <summary>
    /// A command after thirty days away, accepted or refused by a rule: the session's start, then its settlement's completions, then the
    /// command's own event, all in the command's transaction (one write).
    /// </summary>
    /// <param name="x">The tile's column: 1 is free, 0 is taken by the cottage.</param>
    /// <param name="outcome">The command event's outcome.</param>
    [Theory]
    [InlineData(1, "accepted")]
    [InlineData(0, "TILE_OCCUPIED")]
    public async Task ExecuteAsync_AfterCompletions_RecordsTheSettlementsEventsFirst(int x, string outcome)
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromDays(30));

        await Pipeline(repository, clock).ExecuteAsync(
            PlayerId, PlaceCottage() with { X = x, Y = x == 0 ? 0 : 1 }, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                (PlayerEventTypes.SessionStart, (string?)null, (string?)null),
                (PlayerEventTypes.ResearchCompleted, Forestry.Value, null),
                (PlayerEventTypes.BuildingCompleted, "manor", null),
                ("command.place-test", "cottage", outcome),
            ],
            repository.PlayerEvents.Select(e => (e.EventType, e.Detail, e.Outcome)));
        Assert.Equal(1, repository.SaveChangesCount);
    }

    /// <summary>
    /// A command more than the session gap after the last request, accepted or refused: <c>session.start</c> first, at the settled clock and
    /// the command's time, then the command's event; exactly the gap records only the command's.
    /// </summary>
    /// <param name="seconds">Seconds since the last request.</param>
    /// <param name="x">The tile's column: 1 is free, 0 is taken by the cottage.</param>
    /// <param name="expected">The event types, in order.</param>
    [Theory]
    [InlineData(1801, 1, new[] { PlayerEventTypes.SessionStart, "command.place-test" })]
    [InlineData(1801, 0, new[] { PlayerEventTypes.SessionStart, "command.place-test" })]
    [InlineData(1800, 1, new[] { "command.place-test" })]
    [InlineData(1800, 0, new[] { "command.place-test" })]
    public async Task ExecuteAsync_AfterTheGap_RecordsSessionStartFirst(long seconds, int x, string[] expected)
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(seconds));

        await Pipeline(repository, clock).ExecuteAsync(
            PlayerId, PlaceCottage() with { X = x, Y = x == 0 ? 0 : 1 }, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(expected, repository.PlayerEvents.Select(e => e.EventType));
        Assert.All(repository.PlayerEvents, e => Assert.Equal(
            (repository.Stored(PlayerId).State.Clock, clock.GetUtcNow()), (e.CityClock, e.OccurredAt)));
        Assert.Equal(1, repository.SaveChangesCount);
    }

    /// <summary>A replay an hour later is no request that settles: it records no session start.</summary>
    [Fact]
    public async Task ExecuteAsync_ReplayAfterTheGap_RecordsNoSessionStart()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        await Pipeline(repository, clock).ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromHours(1));

        await Pipeline(repository, clock).ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(["command.place-test"], repository.PlayerEvents.Select(e => e.EventType));
    }

    /// <summary>The same request again is a replay: it records nothing more.</summary>
    [Fact]
    public async Task ExecuteAsync_Replay_RecordsNothing()
    {
        InMemoryCityRepository repository = Repository();
        await Pipeline(repository, ClockAtLastSettled()).ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        CommandResult again = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Replayed, again.Outcome);
        Assert.Single(repository.PlayerEvents);
    }

    /// <summary>The same request id with another payload is a conflict: it records nothing more.</summary>
    [Fact]
    public async Task ExecuteAsync_IdempotencyConflict_RecordsNothing()
    {
        InMemoryCityRepository repository = Repository();
        await Pipeline(repository, ClockAtLastSettled()).ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        CommandResult conflict = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage() with { X = 2, Y = 2 }, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.IdempotencyConflict, conflict.Outcome);
        Assert.Single(repository.PlayerEvents);
    }

    /// <summary>A shape refusal never reaches the database (OD-13): no transaction and no event.</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidShape_RecordsNothing()
    {
        InMemoryCityRepository repository = Repository();

        await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage() with { BuildingTypeId = " " }, Writer(repository), TestContext.Current.CancellationToken);

        Assert.Empty(repository.Events);
        Assert.Empty(repository.PlayerEvents);
    }

    /// <summary>An identical request that commits its receipt first rolls this one back whole: its event is not stored.</summary>
    [Fact]
    public async Task ExecuteAsync_IdenticalRequestCommittedFirst_RecordsNothing()
    {
        InMemoryCityRepository repository = Repository();
        repository.BeforeNextWrite = () => repository.AddReceipt(
            PlayerId, new CommandReceipt(RequestId, PlaceTestCommand.Type, CommandPayload.Hash(PlaceCottage()), "{\"winner\":true}"));

        CommandResult result = await Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Replayed, result.Outcome);
        Assert.Empty(repository.PlayerEvents);
    }

    /// <summary>A write that fails (a lost lock) commits nothing: the event is not stored.</summary>
    [Fact]
    public async Task ExecuteAsync_WriteFails_RecordsNothing()
    {
        InMemoryCityRepository repository = Repository();
        repository.FailNextWrite = new InvalidOperationException("The database is gone.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, PlaceCottage(), Writer(repository), TestContext.Current.CancellationToken));

        Assert.Empty(repository.PlayerEvents);
    }
}
