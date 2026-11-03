using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>CityRules.StartResearch</c> (<c>04</c> sections 5, 8 and 16; CR-0008 D1, D11). Fixture numbers: carpentry (era 1) costs 50 gold and takes
/// 100 s; better saws (era 1, needs carpentry) costs 60 gold and 5 boards and takes 200 s; warehouses (era 2, needs better saws) costs 80
/// gold. Research happens at the school (the science building): each city here has a completed one at (5,4) unless a test says otherwise.
/// Only coins are paid at once; a node with goods waits for them at the school.
/// </summary>
public sealed class StartResearchTests
{
    /// <summary>Starting a node of coins only pays them and fills the research slot until clock plus duration.</summary>
    [Fact]
    public void StartResearch_NodeOfCoinsOnly_PaysAndFillsTheSlot()
    {
        CityState city = ResearchCity(clock: 1000, purse: 70);

        RulesAssert.Ok(Start(city, "carpentry"));

        Assert.Equal(
            """
            clock=1000 era=1 next=10 purse=20
            building 9 school (5,4) L1 w=0 Idle 0..null target=0 labor=0/0@0 store: needs:
            completed:
            active: carpentry 1000..1100 needs:
            """.ReplaceLineEndings("\n"),
            CityDump.Describe(city));
    }

    /// <summary>A node with goods names only its coins as spent, nothing as refunded, and leaves every store alone (CR-0008 D1).</summary>
    [Fact]
    public void StartResearch_NodeWithGoods_SpendsOnlyTheCoins()
    {
        CityState city = ResearchCity(purse: 70, completedResearch: ["carpentry"], extra: [Idle(1, "depot", 0, 0, store: [("boards", 9)])]);

        RulesAssert.Moved(Start(city, "better_saws"), spent: "gold=60", refunded: "");

        Assert.Equal((10L, 9L), (city.Purse, city.Buildings[0].GetStock(new ItemId("boards"))));
    }

    /// <summary>
    /// With its prerequisite completed a node with goods starts and waits for them: the slot is filled, no start or end yet, the boards as its
    /// needs (<c>04</c> section 5).
    /// </summary>
    [Fact]
    public void StartResearch_NodeWithGoods_WaitsForThemAtTheScienceBuilding()
    {
        CityState city = ResearchCity(clock: 2000, purse: 60, completedResearch: ["carpentry"]);

        RulesAssert.Ok(Start(city, "better_saws"));

        ActiveResearch research = city.ActiveResearch!;
        Assert.Equal(("better_saws", true, (long?)null, (long?)null), (research.Id.Value, research.IsWaiting, research.StartedAt, research.EndsAt));
        Assert.Equal([("boards", 5L)], research.Needs.Select(need => (need.Key.Value, need.Value)));
        Assert.Equal(0, city.Purse);
    }

    /// <summary>A city of era 2 can start an era-2 node.</summary>
    [Fact]
    public void StartResearch_NodeOfTheCitysLaterEra_IsStarted() =>
        RulesAssert.Ok(Start(ResearchCity(era: 2, purse: 80, completedResearch: ["carpentry", "better_saws"]), "warehouses"));

    /// <summary>
    /// Without a completed science building research can't start: <see cref="MutationErrorCode.NoScienceBuilding"/>, also while one is still
    /// constructing (CR-0008 D11).
    /// </summary>
    [Fact]
    public void StartResearch_NoCompletedScienceBuilding_IsRejected()
    {
        RulesAssert.Rejected(MutationErrorCode.NoScienceBuilding, City(purse: 100), city => Start(city, "carpentry"));
        RulesAssert.Rejected(
            MutationErrorCode.NoScienceBuilding, City(purse: 100, buildings: [Constructing(1, "school", 0, 0, 990, 1005)]), city => Start(city, "carpentry"));
    }

    /// <summary>A node the content doesn't know is rejected as <see cref="MutationErrorCode.UnknownResearch"/>.</summary>
    [Fact]
    public void StartResearch_UnknownNode_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.UnknownResearch, ResearchCity(purse: 100), city => Start(city, "alchemy"));

    /// <summary>A completed node can't run again: <see cref="MutationErrorCode.ResearchCompleted"/>.</summary>
    [Fact]
    public void StartResearch_NodeAlreadyCompleted_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.ResearchCompleted, ResearchCity(purse: 100, completedResearch: ["carpentry"]), city => Start(city, "carpentry"));

    /// <summary>With the one slot busy, also by a research waiting for goods, another node is rejected as <see cref="MutationErrorCode.ResearchInProgress"/>.</summary>
    [Fact]
    public void StartResearch_SlotBusy_IsRejected()
    {
        CityState running = ResearchCity(purse: 100, activeResearch: new ActiveResearch(new ResearchId("carpentry"), 990, 1090));
        CityState waiting = ResearchCity(
            purse: 100, completedResearch: ["carpentry"], activeResearch: new ActiveResearch(new ResearchId("better_saws"), null, null, [new(new ItemId("boards"), 5)]));

        RulesAssert.Rejected(MutationErrorCode.ResearchInProgress, running, city => Start(city, "carpentry"));
        RulesAssert.Rejected(MutationErrorCode.ResearchInProgress, waiting, city => Start(city, "tallying"));
    }

    /// <summary>A node whose prerequisite isn't completed is rejected as <see cref="MutationErrorCode.ResearchLocked"/>.</summary>
    [Fact]
    public void StartResearch_PrerequisiteMissing_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.ResearchLocked, ResearchCity(purse: 100), city => Start(city, "better_saws"));

    /// <summary>A node of a later era than the city's is locked.</summary>
    [Fact]
    public void StartResearch_NodeOfALaterEra_IsRejected() =>
        RulesAssert.Rejected(
            MutationErrorCode.ResearchLocked, ResearchCity(era: 1, purse: 100, completedResearch: ["carpentry", "better_saws"]), city => Start(city, "warehouses"));

    /// <summary>A node of an earlier era is locked too: a city researches only its own era's tree.</summary>
    [Fact]
    public void StartResearch_NodeOfAnEarlierEra_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.ResearchLocked, ResearchCity(era: 2, purse: 100), city => Start(city, "carpentry"));

    /// <summary>Coins the purse can't cover are rejected as <see cref="MutationErrorCode.InsufficientResource"/>; goods don't count.</summary>
    [Fact]
    public void StartResearch_CoinsNotAffordable_IsRejected()
    {
        CityState city = ResearchCity(purse: 59, completedResearch: ["carpentry"], extra: [Idle(1, "depot", 0, 0, store: [("boards", 50)])]);

        RulesAssert.Rejected(MutationErrorCode.InsufficientResource, city, city => Start(city, "better_saws"));
    }

    /// <summary>
    /// When several checks fail, the first in spec order wins: completed before a busy slot, a busy slot before a lock, a lock before a missing
    /// science building, which comes before the cost.
    /// </summary>
    [Fact]
    public void StartResearch_SeveralChecksFail_ReportsTheFirstInSpecOrder()
    {
        CityState busy = City(activeResearch: new ActiveResearch(new ResearchId("carpentry"), 990, 1090));

        // Completed beats a busy slot; a busy slot beats a missing prerequisite and the cost.
        RulesAssert.Rejected(MutationErrorCode.ResearchCompleted, City(completedResearch: ["carpentry"], activeResearch: new ActiveResearch(new ResearchId("better_saws"), 990, 1190)),
            city => Start(city, "carpentry"));
        RulesAssert.Rejected(MutationErrorCode.ResearchInProgress, busy, city => Start(city, "better_saws"));
        RulesAssert.Rejected(MutationErrorCode.ResearchLocked, City(), city => Start(city, "better_saws"));
        RulesAssert.Rejected(MutationErrorCode.NoScienceBuilding, City(), city => Start(city, "carpentry"));
    }

    /// <summary>A null city or content is a programmer error and throws.</summary>
    [Fact]
    public void StartResearch_NullCityOrContent_Throws()
    {
        // The nulls are deliberate: they are programmer errors, not business rules.
        Assert.Throws<ArgumentNullException>(() => CityRules.StartResearch(null!, TestContent, new ResearchId("carpentry")));
        Assert.Throws<ArgumentNullException>(() => CityRules.StartResearch(City(), null!, new ResearchId("carpentry")));
    }

    /// <summary>A test city with the completed school (building 9 at (5,4)) and the given buildings, purse, research and era.</summary>
    private static CityState ResearchCity(
        long clock = 1000, int era = 1, long purse = 0, string[]? completedResearch = null, ActiveResearch? activeResearch = null, Building[]? extra = null) =>
        City(clock: clock, era: era, purse: purse, buildings: [.. extra ?? [], Idle(9, "school", 5, 4)], completedResearch: completedResearch, activeResearch: activeResearch);

    /// <summary>Tries to start a research node, with the fixture content.</summary>
    private static MutationResult Start(CityState city, string research) => CityRules.StartResearch(city, TestContent, new ResearchId(research));
}
