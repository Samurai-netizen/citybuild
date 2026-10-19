using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// Settling a city to the server clock (<c>04</c> sections 1 and 2) over the <see cref="SettleFixture"/> city: at the last settlement its
/// woodcutter had just started a 60-second cycle, the 2-day research <c>forestry</c> was running, and a 3-day manor was under construction.
/// </summary>
public sealed class SettleCityTests
{
    private const int _woodcutterId = 2;

    [Theory]
    [InlineData(0L, 0L, 0L)]
    [InlineData(1L, 1L, 0L)]
    [InlineData(ProductionCapSeconds, ProductionCapSeconds, 480L)] // the cycles ending at 120, 180, …, 28860
    [InlineData(ProductionCapSeconds + 1, ProductionCapSeconds, 480L)]
    public async Task ExecuteAsync_WithinAndJustBeyondTheCap_AdvancesByTheElapsedSeconds(long seconds, long productionSeconds, long logs)
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(seconds));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(seconds, result.Report.ElapsedSeconds);
        Assert.Equal(productionSeconds, result.Report.ProductionSeconds);
        Assert.Equal(logs, result.City.State.GetQuantity(Logs));
        Assert.Equal(StartClock + seconds, result.City.State.Clock);
        Assert.Equal(LastSettled.ToUnixTimeSeconds() + seconds, result.City.LastSettledUnixSec);
        Assert.Empty(result.Report.CompletedResearch);
        Assert.Empty(result.Report.CompletedBuildings);
    }

    [Theory]
    [InlineData(ProductionCapSeconds)]
    [InlineData(ProductionCapSeconds + 1)]
    public async Task ExecuteAsync_ExactlyAtOrJustBeyondTheCap_KeepsTheCycleThatStartedAtTheCap(long seconds)
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(seconds));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        // The cycle ending at the last second of the cap (t = T0 + 8 h) is followed by one more: starts are allowed while t ≤ T0 + cap.
        Building woodcutter = result.City.State.Buildings.Single(building => building.Id == _woodcutterId);
        Assert.Equal(BuildingPhase.Producing, woodcutter.Phase);
        Assert.Equal(StartClock + ProductionCapSeconds, woodcutter.PhaseStartedAt);
    }

    [Fact]
    public async Task ExecuteAsync_ThirtyDaysAway_FinishesTheTimersAndStopsStartingCyclesAfterTheCap()
    {
        const long thirtyDays = 30 * 86_400;
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(thirtyDays));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        CityState city = result.City.State;
        Assert.Equal(thirtyDays, result.Report.ElapsedSeconds);
        Assert.Equal(ProductionCapSeconds, result.Report.ProductionSeconds);
        Assert.Equal(StartClock + thirtyDays, city.Clock);
        Assert.Equal([Forestry], result.Report.CompletedResearch);
        Assert.True(city.HasCompletedResearch(Forestry));
        Assert.Null(city.ActiveResearch);
        Assert.Equal([ManorId], result.Report.CompletedBuildings);
        Assert.Equal(BuildingPhase.Idle, city.Buildings.Single(building => building.Id == ManorId).Phase);

        // 480 cycles end within the cap, and the one that started at its last second ends a minute later; then nothing starts.
        Assert.Equal(481, city.GetQuantity(Logs));
        Assert.Equal(481, result.Report.Produced[Logs]);
        Building woodcutter = city.Buildings.Single(building => building.Id == _woodcutterId);
        Assert.Equal(BuildingPhase.Idle, woodcutter.Phase);
        Assert.Equal(1, woodcutter.AssignedWorkers);
    }

    [Fact]
    public async Task ExecuteAsync_TheSecondTheResearchEnds_CompletesItThenAndNotBefore()
    {
        long secondsToResearchEnd = ResearchEndsAt - StartClock;
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);

        clock.Advance(TimeSpan.FromSeconds(secondsToResearchEnd - 1));
        SettleCityResult before = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        Assert.Empty(before.Report.CompletedResearch);
        Assert.NotNull(before.City.State.ActiveResearch);

        clock.Advance(TimeSpan.FromSeconds(1));
        SettleCityResult at = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        Assert.Equal([Forestry], at.Report.CompletedResearch);
        Assert.Equal(ResearchEndsAt, at.City.State.Clock);
    }

    [Fact]
    public async Task ExecuteAsync_ClockBehindTheLastSettlement_AdvancesNothingAndKeepsTheLastSettledSecond()
    {
        // The city was settled at a second the server clock shows 60 seconds from now: the clock was set back.
        InMemoryCityRepository repository = Repository(lastSettled: LastSettled.AddSeconds(60));
        FakeTimeProvider clock = ClockAtLastSettled();

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.True(result.Window.IsClockBehind);
        Assert.Equal(0, result.Report.ElapsedSeconds);
        Assert.Equal(StartClock, result.City.State.Clock);
        Assert.Equal(LastSettled.ToUnixTimeSeconds() + 60, result.City.LastSettledUnixSec);
    }

    [Fact]
    public async Task ExecuteAsync_ClockCatchesUpAfterGoingBack_CountsOnlyTheSecondsAfterTheLastSettlement()
    {
        InMemoryCityRepository repository = Repository(lastSettled: LastSettled.AddSeconds(60));
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);
        await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(90));
        SettleCityResult result = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.False(result.Window.IsClockBehind);
        Assert.Equal(30, result.Report.ElapsedSeconds);
        Assert.Equal(StartClock + 30, result.City.State.Clock);
    }

    [Fact]
    public async Task ExecuteAsync_HundredCallsThreeHundredMillisecondsApart_ElapseTheSameAsOneCallAtTheLast()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);
        long total = 0;
        SettleCityResult? last = null;

        // Calls at t = 0, 0.3, …, 29.7 s: each floors now to a whole second, and the dropped fractions carry over to the next window.
        for (int call = 0; call < 100; call++)
        {
            if (call > 0)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }

            last = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
            total += last.Report.ElapsedSeconds;
        }

        InMemoryCityRepository once = Repository();
        FakeTimeProvider onceClock = ClockAtLastSettled();
        onceClock.Advance(TimeSpan.FromMilliseconds(29_700));
        SettleCityResult single = await UseCase(once, onceClock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(29, total);
        Assert.Equal(29, single.Report.ElapsedSeconds);
        Assert.Equal(single.City.LastSettledUnixSec, last!.City.LastSettledUnixSec);
        Assert.Equal(Describe(single.City.State), Describe(last.City.State));
    }

    [Fact]
    public void Constructor_NullDependency_Throws()
    {
        var repository = new InMemoryCityRepository();
        FakeTimeProvider clock = ClockAtLastSettled();
        IContentProvider content = new FixedContent(SettleFixture.Content);

        Assert.Throws<ArgumentNullException>("cities", () => new SettleCity(null!, content, clock));
        Assert.Throws<ArgumentNullException>("content", () => new SettleCity(repository, null!, clock));
        Assert.Throws<ArgumentNullException>("timeProvider", () => new SettleCity(repository, content, null!));
    }

    [Fact]
    public async Task ExecuteAsync_AnySettlement_SavesTheCity()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(5));

        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        CityAggregate stored = await repository.LoadForUpdateAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(LastSettled.ToUnixTimeSeconds() + 5, stored.LastSettledUnixSec);
        Assert.Equal(StartClock + 5, stored.State.Clock);
    }
}
