using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Display;

/// <summary><c>ProgressDisplay.SecondsUntilNextEvent</c>: the countdown to the end of a building's job.</summary>
public sealed class SecondsUntilNextEventTests
{
    /// <summary>A running cycle counts down to its end, and stays at 0 after it until the next snapshot arrives.</summary>
    [Theory]
    [InlineData(990L, 20L)]
    [InlineData(1000L, 10L)]
    [InlineData(1010L, 0L)]
    [InlineData(1500L, 0L)] // the end has passed and the next snapshot hasn't arrived
    public void SecondsUntilNextEvent_RunningCycle_CountsToTheEndOfTheCycle(long clock, long expected)
    {
        Building camp = Producing(1, "lumber_camp", 0, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010);

        Assert.Equal(expected, ProgressDisplay.SecondsUntilNextEvent(camp, clock));
    }

    /// <summary>A frozen cycle has no end, so there is no countdown.</summary>
    [Fact]
    public void SecondsUntilNextEvent_FrozenCycle_IsNull()
    {
        Building frozen = Producing(1, "workshop", 0, 0, 1, workers: 0, cycleStartedAt: 960, cycleLabor: 90, laborRemaining: 50, segmentStartedAt: 990, endsAt: null);

        Assert.Null(ProgressDisplay.SecondsUntilNextEvent(frozen, 1000));
    }

    /// <summary>An idle building has no job, so there is no countdown.</summary>
    [Fact]
    public void SecondsUntilNextEvent_IdleBuilding_IsNull()
    {
        Assert.Null(ProgressDisplay.SecondsUntilNextEvent(Idle(1, "workshop", 0, 0, workers: 1), 1000));
    }

    /// <summary>A construction counts down to its end.</summary>
    [Fact]
    public void SecondsUntilNextEvent_Construction_CountsToTheEnd()
    {
        Assert.Equal(7L, ProgressDisplay.SecondsUntilNextEvent(Constructing(1, "workshop", 0, 0, startedAt: 990, endsAt: 1010), 1003));
    }

    /// <summary>An upgrade counts down to its end.</summary>
    [Fact]
    public void SecondsUntilNextEvent_Upgrade_CountsToTheEnd()
    {
        Building upgrading = Upgrading(1, "workshop", 0, 0, level: 1, workers: 1, startedAt: 990, endsAt: 1005);

        Assert.Equal(5L, ProgressDisplay.SecondsUntilNextEvent(upgrading, 1000));
    }

    /// <summary>A null building throws.</summary>
    [Fact]
    public void SecondsUntilNextEvent_NullBuilding_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProgressDisplay.SecondsUntilNextEvent(null!, 1000));
    }
}
