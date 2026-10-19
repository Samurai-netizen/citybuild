using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Golden;

namespace CityBuilder.Simulation.Tests.Settlement;

/// <summary>
/// <c>Simulator.Advance</c> stays allocation-light (<c>.claude/rules/simulation.md</c>): one advance can process ~200k events (<c>04</c>
/// section 12), so nothing it does per event may allocate. Only the report allocates, and its size depends on the items and buildings, not
/// on the number of events.
/// </summary>
public sealed class AdvanceAllocationTests
{
    private const long _shortAdvanceSeconds = 120;

    private const long _eightHours = 8 * 3600;

    [Fact]
    public void Advance_EightHoursOfProduction_AllocatesNoMoreThanTwoMinutes()
    {
        // Golden scenario #1 at t=1170: a woodcutter, a sawmill and a trading post run cycles (with logs, planks and coins in every report)
        // and trade_charter is completed, so every start reads research effects. Eight hours are about 1,700 events, two minutes about 8.
        Simulator.Advance(GoldenScenario1.After(1170, GoldenScenario1.OneAdvancePerGap), GoldenContent.V1, _eightHours);
        CityState shortRun = GoldenScenario1.After(1170, GoldenScenario1.OneAdvancePerGap);
        CityState longRun = GoldenScenario1.After(1170, GoldenScenario1.OneAdvancePerGap);

        long shortBytes = AllocatedBytes(() => Simulator.Advance(shortRun, GoldenContent.V1, _shortAdvanceSeconds));
        long longBytes = AllocatedBytes(() => Simulator.Advance(longRun, GoldenContent.V1, _eightHours));

        Assert.True(longBytes <= shortBytes, $"Eight hours allocated {longBytes} bytes, two minutes {shortBytes}.");
    }

    private static long AllocatedBytes(Action action)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
