using CityBuilder.Application.Cities;
using CityBuilder.Domain.Metrics;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// The settlement's player events (CR-0009) in <see cref="SettleCity"/>: a settle that completes research or construction records one
/// event per completion in its transaction, after the session's start when it starts one; a settle that completes nothing within the
/// session gap, and a second settle at the same instant, record none.
/// </summary>
public sealed class SettleCityEventTests
{
    /// <summary>
    /// Thirty days away: the research and the manor each give one event after the session's start (<see cref="SettleCitySessionTests"/>),
    /// written with the city's save.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CompletionsInTheWindow_RecordsOneEventEach()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromDays(30));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(
            [(PlayerEventTypes.SessionStart, (string?)null), (PlayerEventTypes.ResearchCompleted, Forestry.Value), (PlayerEventTypes.BuildingCompleted, "manor")],
            repository.PlayerEvents.Select(e => (e.EventType, e.Detail)));
        Assert.All(repository.PlayerEvents, e => Assert.Equal((result.City.State.Clock, result.ServerTime), (e.CityClock, e.OccurredAt)));
        Assert.Equal(1, repository.SaveChangesCount);
    }

    /// <summary>A second settle at the same instant completes nothing more and records nothing more (the first recorded three).</summary>
    [Fact]
    public async Task ExecuteAsync_SecondSettleAtTheSameInstant_RecordsNothingMore()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromDays(30));
        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(3, repository.PlayerEvents.Count);
    }

    /// <summary>A settle that only produces, within the session gap, records no event.</summary>
    [Fact]
    public async Task ExecuteAsync_NothingCompleted_RecordsNothing()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromMinutes(20));

        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Empty(repository.PlayerEvents);
    }
}
