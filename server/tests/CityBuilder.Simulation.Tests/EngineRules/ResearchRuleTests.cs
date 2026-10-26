using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// Research (<c>04</c> sections 5 and 6): a completed node counts from its second, before the starts of that second, and only for cycles that
/// start afterwards; it unlocks placement and raises caps. Fixture numbers: a hut provides 3 workers; a lumber camp makes 1 wood from 40 labor
/// (30 with <c>better_saws</c>, −250 ‰) with up to 2 workers (4 at level 2), builds in 10 s and upgrades in 12 s; <c>carpentry</c> (50 gold,
/// 100 s) unlocks the workshop (40 gold and 10 wood); <c>warehouses</c> (era 2) adds 50 to the wood cap of 100.
/// </summary>
public sealed class ResearchRuleTests
{
    /// <summary>The capped item whose cap the storage research raises.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>The three kinds of building completion that can share a second with a research completion.</summary>
    public static TheoryData<string> BuildingCompletions => new() { "construction", "upgrade", "cycle end" };

    /// <summary>A research completing in the same second as a construction, upgrade or cycle end counts for the cycle that starts in that second.</summary>
    [Theory]
    [MemberData(nameof(BuildingCompletions))]
    public void ResearchCompletion_DueWithABuildingCompletion_CountsBeforeThatSecondsStarts(string completion)
    {
        // better_saws and the camp's construction, upgrade or cycle end are all due at 1020.
        Building camp = completion switch
        {
            "construction" => Constructing(2, "lumber_camp", 1, 0, startedAt: 1010, endsAt: 1020),
            "upgrade" => Upgrading(2, "lumber_camp", 1, 0, level: 1, workers: 2, startedAt: 1008, endsAt: 1020),
            _ => Producing(2, "lumber_camp", 1, 0, 1, workers: 2, cycleStartedAt: 1000, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 1000, endsAt: 1020),
        };
        CityState city = City(clock: 1010, buildings: [Idle(1, "hut", 0, 0), camp], completedResearch: ["carpentry"],
            activeResearch: new ActiveResearch(new ResearchId("better_saws"), 820, 1020));

        SettlementReport report = Simulator.Advance(city, TestContent, 10);

        // The cycle that starts at 1020 already has the 30 labor of better_saws: 15 s with 2 workers. Whether the research or the building
        // completes first can't change this with the M0 effect types, none of which changes a completion (M0-09 decision 19).
        Building started = city.Buildings[1];
        Assert.Equal((BuildingPhase.Producing, 1020L, (long?)1035, 30L), (started.Phase, started.PhaseStartedAt, started.PhaseEndsAt, started.CycleLabor));
        Assert.Equal([new ResearchId("better_saws")], report.CompletedResearch);
    }

    /// <summary>A labor reduction completing mid-cycle leaves the running cycle's labor as it is, even through a worker change; the next cycle gets it.</summary>
    [Fact]
    public void ResearchEffects_CompleteMidCycle_LeaveTheRunningCycleAlone()
    {
        // better_saws completes at 1010, halfway through the level-2 camp's 40-labor cycle.
        CityState city = City(clock: 1000, completedResearch: ["carpentry"], activeResearch: new ActiveResearch(new ResearchId("better_saws"), 810, 1010),
            buildings: [Idle(1, "hut", 0, 0), Idle(2, "hut", 1, 0), Idle(3, "lumber_camp", 2, 0, level: 2, workers: 2)]);
        Simulator.Advance(city, TestContent, 10);
        Assert.Equal("building 3 lumber_camp (2,0) L2 w=2 Producing 1000..1020 target=0 labor=40/40@1000", CityDump.BuildingLine(city, 3));

        Commands.AssignWorkers(city, 3, 4);

        // A worker change still counts from 40: 20 labor are left (not 15), 5 s with 4 workers. The next cycle has 30: ceil(30 / 4) = 8 s.
        Assert.Equal("building 3 lumber_camp (2,0) L2 w=4 Producing 1000..1015 target=0 labor=40/20@1010", CityDump.BuildingLine(city, 3));
        Simulator.Advance(city, TestContent, 5);
        Assert.Equal("building 3 lumber_camp (2,0) L2 w=4 Producing 1015..1023 target=0 labor=30/30@1015", CityDump.BuildingLine(city, 3));
    }

    /// <summary>An unlock applies from the second its research completes: one second earlier the building is still locked.</summary>
    [Fact]
    public void Unlock_ResearchCompletes_EnablesPlacementFromThatSecond()
    {
        CityState city = City(clock: 1000, inventory: [("gold", 90), ("wood", 10)]);
        RulesAssert.Rejected(MutationErrorCode.BuildingLocked, city, PlaceWorkshop);
        Commands.StartResearch(city, "carpentry");

        // Carpentry is done at 1100: one second earlier the workshop is still locked.
        Simulator.Advance(city, TestContent, 99);
        RulesAssert.Rejected(MutationErrorCode.BuildingLocked, city, PlaceWorkshop);
        Simulator.Advance(city, TestContent, 1);

        Commands.PlaceBuilding(city, "workshop", 0, 0);
        Assert.Equal("workshop", Assert.Single(city.Buildings).TypeId.Value);
    }

    /// <summary>A storage bonus raises the cap from its research's second: a producer waiting on a full stock starts then, and stops at the new cap.</summary>
    [Fact]
    public void StorageBonus_ResearchCompletes_RaisesTheCapFromThatSecond()
    {
        // Wood is at its cap of 100, so the camp waits; warehouses completes at 1030 in this era-2 city.
        CityState city = City(clock: 1000, era: 2, inventory: [("wood", 100)], completedResearch: ["carpentry", "better_saws"],
            activeResearch: new ActiveResearch(new ResearchId("warehouses"), 730, 1030),
            buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2)]);
        Simulator.Advance(city, TestContent, 29);
        Assert.Equal("StorageFull:wood", StallReason.Of(city, TestContent, city.Buildings[1])?.ToString());

        Simulator.Advance(city, TestContent, 1);

        // The camp starts in the second the cap becomes 150 (30 labor with better_saws: 15 s), and stops when the stock reaches it.
        Assert.Equal("building 2 lumber_camp (1,0) L1 w=2 Producing 1030..1045 target=0 labor=30/30@1030", CityDump.BuildingLine(city, 2));
        Simulator.Advance(city, TestContent, 1000);
        Assert.Equal((150L, "StorageFull:wood"), (city.GetQuantity(_wood), StallReason.Of(city, TestContent, city.Buildings[1])?.ToString()));
    }

    /// <summary>Tries to place the locked workshop on tile (0, 0).</summary>
    private static MutationResult PlaceWorkshop(CityState city) => CityRules.PlaceBuilding(city, TestContent, new BuildingTypeId("workshop"), 0, 0);
}
