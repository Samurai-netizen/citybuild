using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Settlement;

/// <summary>
/// The loop of <c>Simulator.Advance</c> (<c>04</c> sections 2, 7 and 11): the clock, the window, the target time and bad input. A hut takes 5 s
/// to build.
/// </summary>
public sealed class AdvanceTests
{
    /// <summary>With nothing running, the clock still moves by the full elapsed time and the report says so.</summary>
    [Fact]
    public void Advance_NothingRunning_MovesTheClockByTheFullElapsedTime()
    {
        CityState city = City(clock: 1000);

        SettlementReport report = Simulator.Advance(city, TestContent, 50);

        Assert.Equal(1050, city.Clock);
        Assert.Equal((50L, 0L), (report.ElapsedSeconds, report.CourierSeconds));
        Assert.Empty(report.TiredCouriers);
        Assert.Empty(report.Produced);
        Assert.Empty(report.Consumed);
        Assert.Empty(report.CompletedBuildings);
        Assert.Empty(report.CompletedResearch);
        Assert.False(report.EraChanged);
    }

    /// <summary>A long advance (30 days) moves the clock by all of it: there is no offline cap (CR-0008, <c>04</c> section 2).</summary>
    [Fact]
    public void Advance_ThirtyDays_MovesTheClockByAllOfIt()
    {
        CityState city = City(clock: 1000);

        SettlementReport report = Simulator.Advance(city, TestContent, 30 * 86_400);

        Assert.Equal(1000 + (30 * 86_400), city.Clock);
        Assert.Equal((30 * 86_400L, 0L), (report.ElapsedSeconds, report.CourierSeconds));
    }

    /// <summary>A zero-length advance keeps the clock.</summary>
    [Fact]
    public void Advance_ZeroElapsed_KeepsTheClock()
    {
        CityState city = City(clock: 1000, buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 0);

        Assert.Equal(1000, city.Clock);
        Assert.Equal((0L, 0L), (report.ElapsedSeconds, report.CourierSeconds));
        Assert.Equal(BuildingPhase.Constructing, city.Buildings[0].Phase);
    }

    /// <summary>A negative elapsed time counts as 0: the clock never moves back.</summary>
    [Fact]
    public void Advance_NegativeElapsed_CountsAsZero()
    {
        CityState city = City(clock: 1000, buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003)]);
        string before = CityDump.Describe(city);

        SettlementReport report = Simulator.Advance(city, TestContent, -5);

        Assert.Equal(before, CityDump.Describe(city));
        Assert.Equal((0L, 0L), (report.ElapsedSeconds, report.CourierSeconds));
    }

    /// <summary>A timer ending exactly at the target completes in this advance.</summary>
    [Fact]
    public void Advance_TimerExactlyAtTheTarget_CompletesIt()
    {
        CityState city = City(clock: 1000, buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1005)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 5);

        Assert.Equal(BuildingPhase.Idle, city.Buildings[0].Phase);
        Assert.Equal([1], report.CompletedBuildings);
        Assert.Equal(1005, city.Clock);
    }

    /// <summary>A timer ending one second after the target keeps running.</summary>
    [Fact]
    public void Advance_TimerOneSecondAfterTheTarget_KeepsItRunning()
    {
        CityState city = City(clock: 1000, buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1005)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 4);

        Assert.Equal("building 1 hut (0,0) L1 w=0 Constructing 998..1005 target=0 labor=0/0@0", CityDump.BuildingLine(city, 1));
        Assert.Empty(report.CompletedBuildings);
        Assert.Equal(1004, city.Clock);
    }

    /// <summary>The timers a restored city may have due before its clock: a construction and a research.</summary>
    public static TheoryData<string> TimersBeforeTheClock => new() { "construction", "research" };

    /// <summary>A timer due before the clock makes the city invalid: the advance throws before it changes anything.</summary>
    [Theory]
    [MemberData(nameof(TimersBeforeTheClock))]
    public void Advance_TimerDueBeforeTheClock_ThrowsAndChangesNothing(string timer)
    {
        CityState city = timer == "construction"
            ? City(clock: 1000, buildings: [Constructing(1, "hut", 0, 0, startedAt: 990, endsAt: 999)])
            : City(clock: 1000, activeResearch: new ActiveResearch(new ResearchId("carpentry"), 800, 900));
        string before = CityDump.Describe(city);

        Assert.Throws<InvalidOperationException>(() => Simulator.Advance(city, TestContent, 10));
        Assert.Equal(before, CityDump.Describe(city));
    }

    /// <summary>A timer due exactly at the clock completes before cycles start there (decision 033), so a research ending then already counts.</summary>
    [Fact]
    public void Advance_TimerDueAtTheClock_CompletesBeforeTheStarts()
    {
        // A restored city whose research ends at its clock: the order of 04 section 6 holds at the clock too, so the camp's cycle starts with
        // better_saws' 25 % off (40 → 30 labor, 15 s with 2 workers), not before it.
        CityState city = City(
            clock: 1000,
            buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2)],
            completedResearch: ["carpentry"],
            activeResearch: new ActiveResearch(new ResearchId("better_saws"), 800, 1000));

        SettlementReport report = Simulator.Advance(city, TestContent, 0);

        Assert.Equal([new ResearchId("better_saws")], report.CompletedResearch);
        Assert.Equal("building 2 lumber_camp (1,0) L1 w=2 Producing 1000..1015 target=0 labor=30/30@1000", CityDump.BuildingLine(city, 2));
    }

    /// <summary>A null city or content is a programmer error and throws.</summary>
    [Fact]
    public void Advance_NullCityOrContent_Throws()
    {
        // The nulls are deliberate: they are programmer errors, not business rules.
        Assert.Throws<ArgumentNullException>("city", () => Simulator.Advance(null!, TestContent, 1));
        Assert.Throws<ArgumentNullException>("content", () => Simulator.Advance(City(), null!, 1));
    }
}
