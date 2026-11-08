using CityBuilder.Application.Cities;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// Presence in the settle (<c>04</c> section 2; CR-0008-T1 part H): the settle passes the engine the city-clock second the player counts as
/// away from (their last-seen second plus the 300 s grace of <see cref="SettleFixture.Presence"/>), and records the request as their new
/// last-seen second. Each expectation is the engine's own advance of the same city with that <c>absentFrom</c>, so the tests check the
/// server's second, not the engine's walks. The test city: the two-sawmill city of <see cref="Commands.PipelineFixture.TwoSawmillsRepository"/>
/// at clock 180 with courier station 6 at (1,1) and courier 6/1 on the route woodcutter 2 → sawmill 5, settled at
/// <see cref="SettleFixture.LastSettled"/> and seen then.
/// </summary>
public sealed class SettleCityPresenceTests
{
    /// <summary>The city clock of the test city, at <see cref="SettleFixture.LastSettled"/>.</summary>
    private const long _clock = 180;

    /// <summary>
    /// After 1 000 s away the courier spent stamina from 300 s after the last request on, exactly as the engine advances the city with
    /// <c>absentFrom</c> at clock 480; the settlement's courier seconds say so.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_LongAbsence_SpendsStaminaFromTheGraceOn()
    {
        InMemoryCityRepository repository = RepositoryWith(RoutedCity());
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(1000));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        CityState expected = RoutedCity();
        SettlementReport report = Simulator.Advance(expected, SettleFixture.Content, 1000, absentFrom: _clock + 300);
        Assert.Equal(CourierLine(expected), CourierLine(result.City.State));
        Assert.True(Courier(result.City.State).Stamina < 14_400, CourierLine(result.City.State));
        Assert.Equal(report.CourierSeconds, result.Settlement.CourierSeconds);
    }

    /// <summary>A settle within the grace (300 s after the last request) spends no stamina: the player still counts as present.</summary>
    [Fact]
    public async Task ExecuteAsync_WithinTheGrace_SpendsNoStamina()
    {
        InMemoryCityRepository repository = RepositoryWith(RoutedCity());
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(300));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(14_400, Courier(result.City.State).Stamina);
        Assert.Equal(0, result.Settlement.CourierSeconds);
    }

    /// <summary>
    /// The settle records the request as the last-seen second and saves it; the next settle, 1 000 s later, counts the player away from 300 s
    /// after that request (clock 480 + 300), as the engine does with that <c>absentFrom</c> after the same first advance.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TwoRequests_TheSecondCountsTheAbsenceFromTheFirst()
    {
        InMemoryCityRepository repository = RepositoryWith(RoutedCity());
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);
        clock.Advance(TimeSpan.FromSeconds(300));
        SettleCityResult first = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1000));

        SettleCityResult second = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.True(first.Saved);
        Assert.Equal(LastSettled.ToUnixTimeSeconds() + 1300, repository.Stored(PlayerId).LastSeenUnixSec);
        CityState expected = RoutedCity();
        Simulator.Advance(expected, SettleFixture.Content, 300, absentFrom: _clock + 300);
        Simulator.Advance(expected, SettleFixture.Content, 1000, absentFrom: _clock + 300 + 300);
        Assert.Equal(CourierLine(expected), CourierLine(second.City.State));
    }

    /// <summary>
    /// A second settle at the last-seen second changes nothing: the clock, the last-seen second and the city stay, and nothing is saved (the
    /// first one sends the routed courier off, which is a change).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TwiceAtTheSameSecond_TheSecondSavesNothing()
    {
        InMemoryCityRepository repository = RepositoryWith(RoutedCity());
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);
        await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        SettleCityResult result = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.False(result.Saved);
        Assert.Equal(LastSettled.ToUnixTimeSeconds(), result.City.LastSeenUnixSec);
    }

    /// <summary>
    /// The absence starts at <c>lastSeen + grace − lastSettled</c> after the city clock: 300 s after it when the player was last seen at the
    /// last settlement, at the clock itself when they were last seen 600 s before it (the grace is over before the window), and never before
    /// the clock.
    /// </summary>
    /// <param name="seenBeforeSettled">How many seconds before the last settlement the player was last seen.</param>
    /// <param name="expected">The expected <c>absentFrom</c>.</param>
    [Theory]
    [InlineData(0, _clock + 300)]
    [InlineData(100, _clock + 200)]
    [InlineData(600, _clock)]
    public void AbsentFrom_LastSeenAndLastSettled_GivesTheCityClockSecond(long seenBeforeSettled, long expected)
    {
        long settled = LastSettled.ToUnixTimeSeconds();
        var city = new CityAggregate(CityId, PlayerId, RoutedCity(), [], settled, 0, SettleFixture.Content.ContentVersion, settled - seenBeforeSettled);

        Assert.Equal(expected, Presence.AbsentFrom(city));
    }

    /// <summary>A negative grace is no policy.</summary>
    [Fact]
    public void Constructor_NegativeGrace_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PresencePolicy(-1));

    /// <summary>The last-seen second never goes back: a request at an earlier server second (a clock set back) keeps it.</summary>
    [Fact]
    public void MarkSeen_EarlierSecond_KeepsTheLastSeen()
    {
        long settled = LastSettled.ToUnixTimeSeconds();
        var city = new CityAggregate(CityId, PlayerId, RoutedCity(), [], settled, 0, SettleFixture.Content.ContentVersion);

        city.MarkSeen(settled - 60);

        Assert.Equal(settled, city.LastSeenUnixSec);
    }

    /// <summary>The test city of the class summary, with courier 6/1 at its station and its route set, as a new object each time.</summary>
    /// <returns>The city at clock 180.</returns>
    private static CityState RoutedCity()
    {
        CityState sawmills = Commands.PipelineFixture.TwoSawmillsRepository().Stored(PlayerId).State;
        var station = new Building(6, new BuildingTypeId("courier_station"), 1, 1, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        var city = new CityState(
            sawmills.Clock,
            sawmills.Era,
            7,
            sawmills.Purse,
            [.. sawmills.Buildings, station],
            [],
            activeResearch: null,
            couriers: [new Courier(6, 1, CourierState.AtStation, 0, null, null, 14_400)],
            routes: [new Route(6, 1,
        [
            new RouteStop(2, [new RouteAction(RouteActionKind.Take, Logs, Amount.Preset(new AmountPresetId("all")))]),
            new RouteStop(5, [new RouteAction(RouteActionKind.Put, Logs, Amount.Preset(new AmountPresetId("all")))]),
        ])]);
        return city;
    }

    /// <summary>Courier 6/1 of the city.</summary>
    private static Courier Courier(CityState city) => city.Couriers.Single(courier => courier.StationId == 6 && courier.Slot == 1);

    /// <summary>Courier 6/1 as text: state, walk, stamina and load, so a difference shows in one assertion.</summary>
    private static string CourierLine(CityState city)
    {
        Courier courier = Courier(city);
        return $"{courier.State} to={courier.ToStop} {courier.DepartedAt}..{courier.ArrivesAt} stamina={courier.Stamina} load="
            + string.Join(",", courier.Loads.Select(load => $"{load.Item}@{load.FromId}={load.Quantity}"));
    }
}
