using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The research and labor rules: prerequisites form no cycle and are never in a later era; the labor reductions of one building type stay
/// within the limit; with every reduction applied and all max workers, no cycle is shorter than the minimum. In the fixture, better_saws
/// requires carpentry and warehouses (era 2) requires better_saws.
/// </summary>
public sealed class ContentResearchRuleTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    [Fact]
    public void Create_TwoNodesRequiringEachOther_ReportsResearchCycle()
    {
        GameContentData data = ValidWith(data => Research(data, "carpentry").Prerequisites = ["better_saws"]);

        ContentAssert.SingleError(data, ContentErrorCode.ResearchCycle, "research[carpentry].prerequisites[better_saws]");
    }

    [Fact]
    public void Create_NodeRequiringItself_ReportsResearchCycle()
    {
        GameContentData data = ValidWith(data => Research(data, "carpentry").Prerequisites = ["carpentry"]);

        ContentAssert.SingleError(data, ContentErrorCode.ResearchCycle, "research[carpentry].prerequisites[carpentry]");
    }

    [Fact]
    public void Create_CycleThroughThreeNodes_ReportsOneResearchCycleWithThePath()
    {
        GameContentData data = ValidWith(data =>
        {
            Research(data, "warehouses").Era = 1;
            Research(data, "carpentry").Prerequisites = ["warehouses"];
        });

        ContentAssert.SingleError(data, ContentErrorCode.ResearchCycle, "research[warehouses].prerequisites[better_saws]");
        Assert.Contains("better_saws → carpentry → warehouses → better_saws", Assert.Single(GameContentFactory.Create(data).Errors).Message);
    }

    [Fact]
    public void Create_PrerequisiteInALaterEra_ReportsPrerequisiteInLaterEra()
    {
        GameContentData data = ValidWith(data =>
        {
            Research(data, "warehouses").Prerequisites = null;
            Research(data, "carpentry").Prerequisites = ["warehouses"];
        });

        ContentAssert.SingleError(data, ContentErrorCode.PrerequisiteInLaterEra, "research[carpentry].prerequisites[warehouses]");
    }

    [Fact]
    public void Create_SharedPrerequisitesWithoutACycle_AreAccepted()
    {
        // A diamond: warehouses requires better_saws and carpentry, and better_saws requires carpentry too.
        GameContentData data = ValidWith(data => Research(data, "warehouses").Prerequisites = ["better_saws", "carpentry"]);

        ContentAssert.Valid(data);
    }

    [Fact]
    public void Create_LaborReductionsAddingUpAboveTheLimit_ReportsLaborReductionTooHigh()
    {
        // lumber_camp: 250 from better_saws + 200 from warehouses = 450 permille, above the fixture's limit of 400.
        GameContentData data = ValidWith(data => Research(data, "warehouses").Effects!.Add(
            new ResearchEffectData { Type = "laborReduction", Building = "lumber_camp", Permille = 200 }));

        ContentAssert.SingleError(data, ContentErrorCode.LaborReductionTooHigh, "buildings[lumber_camp]");
    }

    [Fact]
    public void Create_LaborReductionsAddingUpToExactlyTheLimit_AreAccepted()
    {
        GameContentData data = ValidWith(data => Research(data, "warehouses").Effects!.Add(
            new ResearchEffectData { Type = "laborReduction", Building = "lumber_camp", Permille = 150 }));

        ContentAssert.Valid(data);
    }

    [Fact]
    public void Create_CycleShorterThanTheMinimumWithEveryReduction_ReportsCycleTooShort()
    {
        // lumber_camp level 2: 40 labor − 250 permille = 30, with 3 workers 10 s, below a minimum of 11 s.
        GameContentData data = ValidWith(data => RulesOf(data).MinCycleSeconds = 11);

        ContentAssert.SingleError(data, ContentErrorCode.CycleTooShort, "buildings[lumber_camp].levels[2]");
    }

    [Fact]
    public void Create_CycleExactlyAtTheMinimum_IsAccepted()
    {
        GameContentData data = ValidWith(data => RulesOf(data).MinCycleSeconds = 10);

        ContentAssert.Valid(data);
    }

    [Fact]
    public void Create_SameMinimumWithoutTheReduction_IsAccepted()
    {
        // The counterpart of the test above: without better_saws the level 2 cycle takes CeilDiv(40, 3) = 14 s, so the error there comes
        // from applying the reduction.
        GameContentData withoutReduction = ValidWith(data =>
        {
            RulesOf(data).MinCycleSeconds = 11;
            Research(data, "better_saws").Effects!.RemoveAt(0);
        });

        ContentAssert.Valid(withoutReduction);
    }

    [Fact]
    public void Create_ShortCycleOnALowerLevelOnly_ReportsCycleTooShortForThatLevel()
    {
        // Level 1 has its own, cheaper recipe: 20 labor − 250 permille = 15, with 2 workers 8 s, below 9 s. Level 2 takes 10 s.
        GameContentData data = ValidWith(data =>
        {
            RulesOf(data).MinCycleSeconds = 9;
            Level(data, "lumber_camp", 1).Recipe!.Labor = 20;
        });

        ContentAssert.SingleError(data, ContentErrorCode.CycleTooShort, "buildings[lumber_camp].levels[1]");
    }

    [Fact]
    public void Create_FullReductionOfOneThousandPermille_ReportsCycleTooShortNotAValueError()
    {
        // 1000 permille is the largest value one effect may have; it leaves 0 labor, so the cycle would take no time at all.
        GameContentData data = ValidWith(data =>
        {
            RulesOf(data).MaxLaborReductionPermille = 1000;
            RulesOf(data).MinCycleSeconds = 1;
            Research(data, "better_saws").Effects![1]!.Permille = 1000;
        });

        ContentAssert.SingleError(data, ContentErrorCode.CycleTooShort, "buildings[workshop].levels[1]");
    }
}
