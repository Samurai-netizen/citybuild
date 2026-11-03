using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Settlement;

/// <summary>
/// <c>Simulator.Advance</c> stays allocation-light (<c>.claude/rules/simulation.md</c>): one advance can process ~200k events (<c>04</c>
/// section 12), so nothing it does per event may allocate. Only the report allocates, and its size depends on the items and buildings, not
/// on the number of events.
/// </summary>
public sealed class AdvanceAllocationTests
{
    /// <summary>The short advance whose allocations are the baseline: two minutes, about 30 events of the test city.</summary>
    private const long _shortAdvanceSeconds = 120;

    /// <summary>
    /// The long advance: eight hours, about 820 events of the test city (each mint ends a cycle every 10 s and the lumber camp every 20 s until
    /// its store is full; the content's 1 h cap starts no cycle after the first hour).
    /// </summary>
    private const long _eightHours = 8 * 3600;

    /// <summary>Eight hours of production allocate no more than two minutes do, so nothing allocates per event (after a warm-up run).</summary>
    [Fact]
    public void Advance_EightHoursOfProduction_AllocatesNoMoreThanTwoMinutes()
    {
        // Two mints (the currency goes into the purse, which has no limit) and a lumber camp with a large store run cycles in every second
        // of the window, and carpentry is completed, so every start reads research effects. (Golden scenario #1 on content v1 served here
        // until CR-0008: on the new model only a currency producer runs for hours without couriers; BL-20261102-07, decision 32.)
        Simulator.Advance(Running(), TestContent, _eightHours);
        CityState shortRun = Running();
        CityState longRun = Running();

        long shortBytes = AllocatedBytes(() => Simulator.Advance(shortRun, TestContent, _shortAdvanceSeconds));
        long longBytes = AllocatedBytes(() => Simulator.Advance(longRun, TestContent, _eightHours));

        Assert.True(longBytes <= shortBytes, $"Eight hours allocated {longBytes} bytes, two minutes {shortBytes}.");
    }

    /// <summary>A city at clock 1000 whose two huts staff two mints (2 workers each) and a lumber camp (2 workers), all idle and able to start.</summary>
    private static CityState Running() => City(
        purse: 50,
        buildings:
        [
            Idle(1, "hut", 0, 0),
            Idle(2, "hut", 1, 0),
            Idle(3, "mint", 2, 0, workers: 2),
            Idle(4, "mint", 3, 0, workers: 2),
            Idle(5, "lumber_camp", 4, 0, workers: 2),
        ],
        completedResearch: ["carpentry"]);

    /// <summary>The bytes the current thread allocated while running the action.</summary>
    private static long AllocatedBytes(Action action)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
