using CityBuilder.Application.Cities;
using CityBuilder.Domain.Metrics;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// <c>session.start</c> (CR-0009) in <see cref="SettleCity"/>, with the fixture's 1 800 s gap (<see cref="SettleFixture.Sessions"/>): a
/// settle more than the gap after the player's last-seen second records it first, at the settled clock and the request's time, in the
/// settle's transaction; a settle within the gap records none. The last-seen second is the one before this settle marks the city seen.
/// </summary>
public sealed class SettleCitySessionTests
{
    /// <summary>A settle 1 801 s after the last request starts a session: one event, saved with the city (its last-seen second changed).</summary>
    [Fact]
    public async Task ExecuteAsync_MoreThanTheGapAway_RecordsSessionStart()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(1801));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        PlayerEvent started = Assert.Single(repository.PlayerEvents);
        Assert.Equal(
            (PlayerId, PlayerEventTypes.SessionStart, (string?)null, (string?)null, (long?)null, result.City.State.Clock, result.ServerTime),
            (started.PlayerId, started.EventType, started.Detail, started.Outcome, started.Value, started.CityClock, started.OccurredAt));
        Assert.Equal((1, 1), (repository.SaveCount, repository.SaveChangesCount));
    }

    /// <summary>Exactly the gap, and a second settle right after a session start, are the same session: no event.</summary>
    [Fact]
    public async Task ExecuteAsync_WithinTheGap_RecordsNothing()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(1800));

        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1800));
        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Empty(repository.PlayerEvents);
    }

    /// <summary>The gap counts from the last request, not from the first of the session: 1 801 s after the second settle starts a new one.</summary>
    [Fact]
    public async Task ExecuteAsync_GapAfterTheLastRequest_StartsTheNextSession()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(1801));
        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1000));
        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(1801));
        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal([PlayerEventTypes.SessionStart, PlayerEventTypes.SessionStart], repository.PlayerEvents.Select(e => e.EventType));
    }

    /// <summary>Thirty days away: the session start comes first, before the settlement's completions.</summary>
    [Fact]
    public async Task ExecuteAsync_SessionWithCompletions_RecordsSessionStartFirst()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromDays(30));

        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(
            [PlayerEventTypes.SessionStart, PlayerEventTypes.ResearchCompleted, PlayerEventTypes.BuildingCompleted],
            repository.PlayerEvents.Select(e => e.EventType));
    }
}
