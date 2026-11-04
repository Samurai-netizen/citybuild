using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Simulation;
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
    /// <summary>The woodcutter's building id: it is the second building placed.</summary>
    private const int _woodcutterId = 2;

    /// <summary>
    /// The city advances by the elapsed whole seconds and produces for all of them, with no cap (CR-0008, <c>04</c> section 2): one log per
    /// minute after 0 s, 1 s, 8 h (480 logs) and 8 h and a second, until the woodcutter's store of 1 000 is full (after 60 000 s and after
    /// 90 000 s), with nothing completed yet.
    /// </summary>
    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(1L, 0L)]
    [InlineData(8 * 3600L, 480L)] // the cycles ending at 120, 180, …, 28860
    [InlineData((8 * 3600L) + 1, 480L)]
    [InlineData(60_000L, WoodcutterStoreLimit)]
    [InlineData(90_000L, WoodcutterStoreLimit)]
    public async Task ExecuteAsync_ElapsedSeconds_AdvancesByThemAndProducesUntilTheStoreIsFull(long seconds, long logs)
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(seconds));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(seconds, result.Settlement.ElapsedSeconds);
        Assert.Equal(logs, Total(result.City.State, Logs));
        Assert.Equal(StartClock + seconds, result.City.State.Clock);
        Assert.Equal(LastSettled.ToUnixTimeSeconds() + seconds, result.City.LastSettledUnixSec);
        Assert.Empty(result.Settlement.CompletedResearch);
        Assert.Empty(result.Settlement.CompletedBuildings);
    }

    /// <summary>
    /// The woodcutter keeps producing past 8 h (the cap CR-0008 removed): at 8 h and a second it runs the cycle that started at 8 h, and
    /// once its store is full it starts no more and stands idle.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PastEightHoursAndAtAFullStore_KeepsProducingThenStops()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);

        clock.Advance(TimeSpan.FromSeconds((8 * 3600) + 1));
        SettleCityResult eightHours = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        Building running = eightHours.City.State.Buildings.Single(building => building.Id == _woodcutterId);
        Assert.Equal(BuildingPhase.Producing, running.Phase);
        Assert.Equal(StartClock + (8 * 3600), running.PhaseStartedAt);

        clock.Advance(TimeSpan.FromSeconds(60_000));
        SettleCityResult full = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        Building stopped = full.City.State.Buildings.Single(building => building.Id == _woodcutterId);
        Assert.Equal(BuildingPhase.Idle, stopped.Phase);
        Assert.Equal(WoodcutterStoreLimit, Total(full.City.State, Logs));
    }

    /// <summary>
    /// After 30 days away the research and the manor have completed and the woodcutter filled its store of 1 000 logs, then stopped; the
    /// closing zero-length advance starts nothing, as the store is still full. No courier worked, so no courier seconds.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ThirtyDaysAway_FinishesTheTimersAndProducesUntilTheStoreIsFull()
    {
        const long thirtyDays = 30 * 86_400;
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(thirtyDays));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        CityState city = result.City.State;
        Assert.Equal(thirtyDays, result.Settlement.ElapsedSeconds);
        Assert.Equal(StartClock + thirtyDays, city.Clock);
        Assert.Equal([Forestry], result.Settlement.CompletedResearch);
        Assert.True(city.HasCompletedResearch(Forestry));
        Assert.Null(city.ActiveResearch);
        Assert.Equal([ManorId], result.Settlement.CompletedBuildings);
        Assert.Equal(BuildingPhase.Idle, city.Buildings.Single(building => building.Id == ManorId).Phase);
        Assert.Equal(0, result.Settlement.CourierSeconds);
        Assert.Empty(result.Settlement.TiredCouriers);

        Assert.Equal(WoodcutterStoreLimit, Total(city, Logs));
        Assert.Equal(WoodcutterStoreLimit, result.Settlement.Produced[Logs]);
        Building woodcutter = city.Buildings.Single(building => building.Id == _woodcutterId);
        Assert.Equal(BuildingPhase.Idle, woodcutter.Phase);
        Assert.Equal(1, woodcutter.AssignedWorkers);
    }

    /// <summary>The research completes at its end second, not a second before.</summary>
    [Fact]
    public async Task ExecuteAsync_TheSecondTheResearchEnds_CompletesItThenAndNotBefore()
    {
        long secondsToResearchEnd = ResearchEndsAt - StartClock;
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);

        clock.Advance(TimeSpan.FromSeconds(secondsToResearchEnd - 1));
        SettleCityResult before = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        Assert.Empty(before.Settlement.CompletedResearch);
        Assert.NotNull(before.City.State.ActiveResearch);

        clock.Advance(TimeSpan.FromSeconds(1));
        SettleCityResult at = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        Assert.Equal([Forestry], at.Settlement.CompletedResearch);
        Assert.Equal(ResearchEndsAt, at.City.State.Clock);
    }

    /// <summary>When the server clock is behind the last settlement (it was set back), nothing advances and the last settled second stays.</summary>
    [Fact]
    public async Task ExecuteAsync_ClockBehindTheLastSettlement_AdvancesNothingAndKeepsTheLastSettledSecond()
    {
        // The city was settled at a second the server clock shows 60 seconds from now: the clock was set back.
        InMemoryCityRepository repository = Repository(lastSettled: LastSettled.AddSeconds(60));
        FakeTimeProvider clock = ClockAtLastSettled();

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.True(result.Settlement.Window.IsClockBehind);
        Assert.Equal(60, result.Settlement.ClockBehindSeconds);
        Assert.Equal(0, result.Settlement.ElapsedSeconds);
        Assert.Equal(StartClock, result.City.State.Clock);
        Assert.Equal(LastSettled.ToUnixTimeSeconds() + 60, result.City.LastSettledUnixSec);
        Assert.False(result.Saved);
    }

    /// <summary>When the clock catches up after going back, only the seconds after the last settlement count: 30 of the 90.</summary>
    [Fact]
    public async Task ExecuteAsync_ClockCatchesUpAfterGoingBack_CountsOnlyTheSecondsAfterTheLastSettlement()
    {
        InMemoryCityRepository repository = Repository(lastSettled: LastSettled.AddSeconds(60));
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);
        await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(90));
        SettleCityResult result = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.False(result.Settlement.Window.IsClockBehind);
        Assert.Equal(0, result.Settlement.ClockBehindSeconds);
        Assert.Equal(30, result.Settlement.ElapsedSeconds);
        Assert.Equal(StartClock + 30, result.City.State.Clock);
    }

    /// <summary>
    /// A hundred calls 0.3 s apart elapse 29 s in all and leave the same city as one call at 29.7 s: the dropped fractions of a second carry
    /// over to the next window.
    /// </summary>
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
            total += last.Settlement.ElapsedSeconds;
        }

        InMemoryCityRepository once = Repository();
        FakeTimeProvider onceClock = ClockAtLastSettled();
        onceClock.Advance(TimeSpan.FromMilliseconds(29_700));
        SettleCityResult single = await UseCase(once, onceClock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(29, total);
        Assert.Equal(29, single.Settlement.ElapsedSeconds);
        Assert.Equal(single.City.LastSettledUnixSec, last!.City.LastSettledUnixSec);
        Assert.Equal(Describe(single.City.State), Describe(last.City.State));
    }

    /// <summary>Each null dependency throws for its parameter.</summary>
    [Fact]
    public void Constructor_NullDependency_Throws()
    {
        var runner = new RunnerPerRequest(new InMemoryCityRepository());
        FakeTimeProvider clock = ClockAtLastSettled();
        IContentProvider content = new FixedContent(SettleFixture.Content);

        Assert.Throws<ArgumentNullException>("transactions", () => new SettleCity(null!, content, clock, Presence));
        Assert.Throws<ArgumentNullException>("content", () => new SettleCity(runner, null!, clock, Presence));
        Assert.Throws<ArgumentNullException>("timeProvider", () => new SettleCity(runner, content, null!, Presence));
        Assert.Throws<ArgumentNullException>("presence", () => new SettleCity(runner, content, clock, null!));
    }

    /// <summary>
    /// A research that completed during the settlement gets the clock it completed at, its end, not the clock the settlement reached: left
    /// alone for 30 days, <c>forestry</c> completed at <see cref="ResearchEndsAt"/>.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ResearchCompletedDuringTheWindow_RecordsItsEndAsTheCompletionClock()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromDays(30));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(new Dictionary<ResearchId, long> { [Forestry] = ResearchEndsAt }, result.City.ResearchCompletedAt);
    }

    /// <summary>A settlement that produced and consumed nothing (5 s into a 60 s cycle) stages the city without ledger rows or a receipt, and writes it once.</summary>
    [Fact]
    public async Task ExecuteAsync_NothingProducedOrConsumed_StagesNoLedgerRowsOrReceiptAndWritesOnce()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(5));

        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Empty(repository.LastLedger!);
        Assert.Null(repository.LastReceipt);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    /// <summary>A settlement runs in the city's transaction: it locks and loads the city, saves and writes it, then commits.</summary>
    [Fact]
    public async Task ExecuteAsync_AnySettlement_RunsInTheCityTransaction()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(5));

        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(["begin", $"load {PlayerId}", "save", "write", "commit"], repository.Events);
    }

    /// <summary>Settling saves the city once, with its new clock and last settled second.</summary>
    [Fact]
    public async Task ExecuteAsync_AnySettlement_SavesTheCity()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(5));

        await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        CityAggregate stored = repository.Stored(PlayerId);

        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(LastSettled.ToUnixTimeSeconds() + 5, stored.LastSettledUnixSec);
        Assert.Equal(StartClock + 5, stored.State.Clock);
    }
}
