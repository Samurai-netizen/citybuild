using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// The rules of one production cycle (<c>04</c> sections 3 and 6): its inputs leave the stock when it starts, its outputs arrive when it ends,
/// and it lasts <c>CeilDiv(labor, workers)</c> seconds. Fixture numbers: a hut provides 3 workers; a workshop makes 1 boards from 2 wood, with
/// 90 labor and up to 2 workers at level 1 and 180 labor and up to 6 workers at level 2; <c>better_saws</c> takes 100 ‰ off its labor.
/// </summary>
public sealed class CycleRuleTests
{
    /// <summary>The workshop's input.</summary>
    private static readonly ItemId _wood = new("wood");
    /// <summary>The workshop's output.</summary>
    private static readonly ItemId _boards = new("boards");

    /// <summary>A cycle takes its inputs in the second it starts and nothing more while it runs.</summary>
    [Fact]
    public void Inputs_CycleStarts_LeaveTheStockAtOnce()
    {
        CityState city = WorkshopCity(wood: 3);

        SettlementReport report = Simulator.Advance(city, TestContent, 0);

        // The cycle takes its 2 wood in the second it starts, and nothing more while it runs (until 1045).
        Assert.Equal(new Dictionary<ItemId, long> { [_wood] = 2 }, report.Consumed);
        Assert.Equal(1, city.GetQuantity(_wood));
        Simulator.Advance(city, TestContent, 44);
        Assert.Equal(1, city.GetQuantity(_wood));
    }

    /// <summary>A cycle's outputs are only reserved until its end, arrive in exactly that second, and count as produced then.</summary>
    [Fact]
    public void Outputs_CycleEnds_ArriveInThatSecondAndNotBefore()
    {
        CityState city = WorkshopCity(wood: 2);

        // One second before the end the board is only reserved.
        Simulator.Advance(city, TestContent, 44);
        Assert.Equal((0L, 1L), (city.GetQuantity(_boards), CityQueries.Reserved(city, TestContent, _boards)));

        SettlementReport report = Simulator.Advance(city, TestContent, 1);

        Assert.Equal((1L, 0L), (city.GetQuantity(_boards), CityQueries.Reserved(city, TestContent, _boards)));
        Assert.Equal(new Dictionary<ItemId, long> { [_boards] = 1 }, report.Produced);
    }

    /// <summary>A cycle lasts <c>CeilDiv(labor, workers)</c> seconds, with research reductions applied to the labor first (162 / 4 = 40.5 → 41 s).</summary>
    [Theory]
    [InlineData(4, false, 180, 45)]
    [InlineData(5, false, 180, 36)]
    [InlineData(1, false, 180, 180)]
    [InlineData(4, true, 162, 41)]
    [InlineData(5, true, 162, 33)]
    [InlineData(6, true, 162, 27)]
    public void Duration_CycleStarts_IsTheLaborCeilDivTheWorkers(int workers, bool betterSaws, long labor, long seconds)
    {
        // A level-2 workshop: 180 labor, which divides evenly; with better_saws ceil(180 × 900 / 1000) = 162, and 162 / 4 = 40.5 and
        // 162 / 5 = 32.4 round up.
        string[] research = betterSaws ? ["carpentry", "better_saws"] : ["carpentry"];
        CityState city = City(clock: 1000, inventory: [("wood", 2)], completedResearch: research, buildings:
            [Idle(1, "hut", 0, 0), Idle(2, "hut", 1, 0), Idle(3, "workshop", 2, 0, level: 2, workers: workers)]);

        Simulator.Advance(city, TestContent, 0);

        Building workshop = city.Buildings[2];
        Assert.Equal((labor, (long?)(1000 + seconds)), (workshop.CycleLabor, workshop.PhaseEndsAt));
    }

    /// <summary>A hut and an idle level-1 workshop with 2 workers at 1000: 45 s cycles.</summary>
    private static CityState WorkshopCity(long wood) =>
        City(clock: 1000, inventory: [("wood", wood)], completedResearch: ["carpentry"], buildings: [Idle(1, "hut", 0, 0), Idle(2, "workshop", 1, 0, workers: 2)]);
}
