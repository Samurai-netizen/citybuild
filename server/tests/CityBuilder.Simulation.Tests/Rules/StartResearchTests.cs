using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>CityRules.StartResearch</c> (<c>04</c> sections 5 and 8). Fixture numbers: carpentry (era 1) costs 50 gold and takes 100 s; better saws
/// (era 1, needs carpentry) costs 60 gold and 5 boards and takes 200 s; warehouses (era 2, needs better saws) costs 80 gold.
/// </summary>
public sealed class StartResearchTests
{
    /// <summary>Starting a node pays its whole cost at once and fills the research slot until clock plus duration.</summary>
    [Fact]
    public void StartResearch_ValidNode_PaysTheFullCostUpFrontAndFillsTheSlot()
    {
        CityState city = City(clock: 1000, inventory: [("gold", 70), ("wood", 4)]);

        RulesAssert.Ok(Start(city, "carpentry"));

        Assert.Equal(
            """
            clock=1000 era=1 next=1
            inventory: gold=20 wood=4
            completed:
            active: carpentry 1000..1100
            """.ReplaceLineEndings("\n"),
            CityDump.Describe(city));
    }

    /// <summary>The result names the cost as spent and nothing as refunded.</summary>
    [Fact]
    public void StartResearch_ValidNode_NamesTheCostAsSpentAndNothingRefunded()
    {
        CityState city = City(inventory: [("boards", 5), ("gold", 70)], completedResearch: ["carpentry"]);

        RulesAssert.Moved(Start(city, "better_saws"), spent: "boards=5 gold=60", refunded: "");
    }

    /// <summary>With its prerequisite completed a node starts and pays every item of its cost (gold and boards).</summary>
    [Fact]
    public void StartResearch_PrerequisiteCompleted_PaysEveryItemOfTheCost()
    {
        CityState city = City(clock: 2000, inventory: [("boards", 5), ("gold", 60)], completedResearch: ["carpentry"]);

        RulesAssert.Ok(Start(city, "better_saws"));

        Assert.Empty(city.Inventory);
        Assert.Equal(("better_saws", 2000L, 2200L), (city.ActiveResearch?.Id.Value, city.ActiveResearch?.StartedAt, city.ActiveResearch?.EndsAt));
        Assert.Equal(["carpentry"], city.CompletedResearch.Select(id => id.Value));
    }

    /// <summary>A city of era 2 can start an era-2 node.</summary>
    [Fact]
    public void StartResearch_NodeOfTheCitysLaterEra_IsStarted()
    {
        CityState city = City(era: 2, inventory: [("gold", 80)], completedResearch: ["carpentry", "better_saws"]);

        RulesAssert.Ok(Start(city, "warehouses"));
    }

    /// <summary>A node the content doesn't know is rejected as <see cref="MutationErrorCode.UnknownResearch"/>.</summary>
    [Fact]
    public void StartResearch_UnknownNode_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.UnknownResearch, City(inventory: [("gold", 100)]), city => Start(city, "alchemy"));

    /// <summary>A completed node can't run again: <see cref="MutationErrorCode.ResearchCompleted"/>.</summary>
    [Fact]
    public void StartResearch_NodeAlreadyCompleted_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.ResearchCompleted, City(inventory: [("gold", 100)], completedResearch: ["carpentry"]), city => Start(city, "carpentry"));

    /// <summary>With the one slot busy another node is rejected as <see cref="MutationErrorCode.ResearchInProgress"/>.</summary>
    [Fact]
    public void StartResearch_SlotBusy_IsRejected()
    {
        CityState city = City(inventory: [("boards", 5), ("gold", 100)], activeResearch: new ActiveResearch(new ResearchId("carpentry"), 990, 1090));

        RulesAssert.Rejected(MutationErrorCode.ResearchInProgress, city, city => Start(city, "carpentry"));
    }

    /// <summary>A node whose prerequisite isn't completed is rejected as <see cref="MutationErrorCode.ResearchLocked"/>.</summary>
    [Fact]
    public void StartResearch_PrerequisiteMissing_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.ResearchLocked, City(inventory: [("boards", 5), ("gold", 100)]), city => Start(city, "better_saws"));

    /// <summary>A node of a later era than the city's is locked.</summary>
    [Fact]
    public void StartResearch_NodeOfALaterEra_IsRejected()
    {
        CityState city = City(era: 1, inventory: [("gold", 100)], completedResearch: ["carpentry", "better_saws"]);

        RulesAssert.Rejected(MutationErrorCode.ResearchLocked, city, city => Start(city, "warehouses"));
    }

    /// <summary>A node of an earlier era is locked too: a city researches only its own era's tree.</summary>
    [Fact]
    public void StartResearch_NodeOfAnEarlierEra_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.ResearchLocked, City(era: 2, inventory: [("gold", 100)]), city => Start(city, "carpentry"));

    /// <summary>A cost the stock can't cover is rejected as <see cref="MutationErrorCode.InsufficientResource"/>.</summary>
    [Fact]
    public void StartResearch_CostNotAffordable_IsRejected()
    {
        CityState city = City(inventory: [("boards", 4), ("gold", 100)], completedResearch: ["carpentry"]);

        RulesAssert.Rejected(MutationErrorCode.InsufficientResource, city, city => Start(city, "better_saws"));
    }

    /// <summary>When several checks fail, the first in spec order wins: completed before a busy slot, a busy slot before a lock or the cost.</summary>
    [Fact]
    public void StartResearch_SeveralChecksFail_ReportsTheFirstInSpecOrder()
    {
        CityState busy = City(activeResearch: new ActiveResearch(new ResearchId("carpentry"), 990, 1090));

        // Completed beats a busy slot; a busy slot beats a missing prerequisite and the cost.
        RulesAssert.Rejected(MutationErrorCode.ResearchCompleted, City(completedResearch: ["carpentry"], activeResearch: new ActiveResearch(new ResearchId("better_saws"), 990, 1190)),
            city => Start(city, "carpentry"));
        RulesAssert.Rejected(MutationErrorCode.ResearchInProgress, busy, city => Start(city, "better_saws"));
        RulesAssert.Rejected(MutationErrorCode.ResearchLocked, City(), city => Start(city, "better_saws"));
    }

    /// <summary>A null city or content is a programmer error and throws.</summary>
    [Fact]
    public void StartResearch_NullCityOrContent_Throws()
    {
        // The nulls are deliberate: they are programmer errors, not business rules.
        Assert.Throws<ArgumentNullException>(() => CityRules.StartResearch(null!, TestContent, new ResearchId("carpentry")));
        Assert.Throws<ArgumentNullException>(() => CityRules.StartResearch(City(), null!, new ResearchId("carpentry")));
    }

    /// <summary>Tries to start a research node, with the fixture content.</summary>
    private static MutationResult Start(CityState city, string research) => CityRules.StartResearch(city, TestContent, new ResearchId(research));
}
