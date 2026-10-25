using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Display;

/// <summary>
/// <c>ProgressDisplay.ProgressPermille</c>: the bar of a building's current job. The helper reads only the timer fields, so the buildings here
/// are built with them directly (a lumber camp's 40-labor cycle, a workshop's 90-labor cycle).
/// </summary>
public sealed class ProgressPermilleTests
{
    /// <summary>A running cycle's bar is worker-seconds done out of its labor: 0 before the start, 250 after 10 of 40, full at and after the end.</summary>
    [Theory]
    [InlineData(985L, 0)] // before the cycle started
    [InlineData(990L, 0)]
    [InlineData(995L, 250)] // 10 of 40 worker-seconds
    [InlineData(1000L, 500)]
    [InlineData(1009L, 950)]
    [InlineData(1010L, 1000)]
    [InlineData(5000L, 1000)] // the end has passed and the next snapshot hasn't arrived
    public void ProgressPermille_CycleWithTwoWorkers_CountsWorkerSeconds(long clock, int expected)
    {
        Building camp = Producing(1, "lumber_camp", 0, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010);

        Assert.Equal(expected, ProgressDisplay.ProgressPermille(camp, clock));
    }

    /// <summary>The bar rounds down: 1 of 90 worker-seconds is 11 permille, not 12.</summary>
    [Fact]
    public void ProgressPermille_RoundsDown()
    {
        Building workshop = Producing(1, "workshop", 0, 0, 1, workers: 1, cycleStartedAt: 1000, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 1000, endsAt: 1090);

        // 1 of 90 worker-seconds is 11.1 permille.
        Assert.Equal(11, ProgressDisplay.ProgressPermille(workshop, 1001));
    }

    /// <summary>A frozen cycle's bar stays at what was done before the freeze (40 of 90: 444), however far the clock runs.</summary>
    [Theory]
    [InlineData(990L, 444)] // 40 of 90 worker-seconds were done before the freeze
    [InlineData(1000L, 444)]
    [InlineData(1_000_000_000L, 444)]
    public void ProgressPermille_FrozenCycle_StaysWhereItStopped(long clock, int expected)
    {
        Building frozen = Producing(1, "workshop", 0, 0, 1, workers: 0, cycleStartedAt: 960, cycleLabor: 90, laborRemaining: 50, segmentStartedAt: 990, endsAt: null);

        Assert.Equal(expected, ProgressDisplay.ProgressPermille(frozen, clock));
    }

    /// <summary>After a worker change the bar keeps the labor done before and adds the new workers' share from the segment's start.</summary>
    [Fact]
    public void ProgressPermille_AfterAWorkerChange_CountsFromTheSegmentWithTheNewWorkers()
    {
        // 90 labor, 40 done before the change at 990, 50 left with 2 workers (25 s, ends 1015).
        Building workshop = Producing(1, "workshop", 0, 0, 1, workers: 2, cycleStartedAt: 960, cycleLabor: 90, laborRemaining: 50, segmentStartedAt: 990, endsAt: 1015);

        Assert.Equal(444, ProgressDisplay.ProgressPermille(workshop, 990));
        Assert.Equal(666, ProgressDisplay.ProgressPermille(workshop, 1000)); // 60 of 90
        Assert.Equal(1000, ProgressDisplay.ProgressPermille(workshop, 1015));
    }

    /// <summary>Second by second, the bar never goes back and stays within 0 to 1000.</summary>
    [Fact]
    public void ProgressPermille_NeverDecreasesAsTheClockAdvances()
    {
        Building workshop = Producing(1, "workshop", 0, 0, 1, workers: 2, cycleStartedAt: 960, cycleLabor: 90, laborRemaining: 50, segmentStartedAt: 990, endsAt: 1015);
        int previous = -1;

        for (long clock = 950; clock <= 1030; clock++)
        {
            int current = ProgressDisplay.ProgressPermille(workshop, clock);

            Assert.InRange(current, 0, 1000);
            Assert.True(current >= previous, $"clock {clock}: {current} after {previous}");
            previous = current;
        }
    }

    /// <summary>A clock of <c>long.MaxValue</c> gives a full bar without overflowing.</summary>
    [Fact]
    public void ProgressPermille_FarAwayClock_DoesNotOverflow()
    {
        Building camp = Producing(1, "lumber_camp", 0, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010);

        Assert.Equal(1000, ProgressDisplay.ProgressPermille(camp, long.MaxValue));
    }

    /// <summary>An idle building has no job, so its bar is 0.</summary>
    [Fact]
    public void ProgressPermille_IdleBuilding_IsZero()
    {
        Assert.Equal(0, ProgressDisplay.ProgressPermille(Idle(1, "workshop", 0, 0, workers: 1), 1000));
    }

    /// <summary>A construction's bar counts seconds of the phase: 10 of 20 s is 500, full at and after the end.</summary>
    [Theory]
    [InlineData(980L, 0)]
    [InlineData(990L, 0)]
    [InlineData(1000L, 500)] // 10 of 20 s
    [InlineData(1009L, 950)]
    [InlineData(1010L, 1000)]
    [InlineData(2000L, 1000)]
    public void ProgressPermille_Construction_CountsSecondsOfThePhase(long clock, int expected)
    {
        Building building = Constructing(1, "workshop", 0, 0, startedAt: 990, endsAt: 1010);

        Assert.Equal(expected, ProgressDisplay.ProgressPermille(building, clock));
    }

    /// <summary>An upgrade's bar counts seconds of the phase: 14 of 15 s is 933.</summary>
    [Theory]
    [InlineData(1004L, 933)] // 14 of 15 s
    [InlineData(1005L, 1000)]
    public void ProgressPermille_Upgrade_CountsSecondsOfThePhase(long clock, int expected)
    {
        Building building = Upgrading(1, "workshop", 0, 0, level: 1, workers: 1, startedAt: 990, endsAt: 1005);

        Assert.Equal(expected, ProgressDisplay.ProgressPermille(building, clock));
    }

    /// <summary>A null building throws.</summary>
    [Fact]
    public void ProgressPermille_NullBuilding_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProgressDisplay.ProgressPermille(null!, 1000));
    }
}
