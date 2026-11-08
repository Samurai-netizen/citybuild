using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>
/// The derived values (<c>04</c> sections 3, 5 and 8). Fixture numbers: a hut provides 3 workers at level 1 and 5 at level 2; a lumber camp
/// makes 1 wood from 40 labor; a workshop 1 boards from 2 wood and 90 labor; caps are wood 100 and boards 80.
/// </summary>
public sealed class CityQueriesTests
{
    /// <summary>The capped raw item (cap 100).</summary>
    private static readonly ItemId _wood = new("wood");
    /// <summary>The workshop's capped output (cap 80).</summary>
    private static readonly ItemId _boards = new("boards");
    /// <summary>The uncapped item.</summary>
    private static readonly ItemId _gold = new("gold");

    /// <summary>Only completed houses provide workers: an idle and an upgrading hut count, a constructing hut doesn't, nor does a producer.</summary>
    [Fact]
    public void WorkersProvided_HousesInEveryPhase_CountsOnlyCompletedHouses()
    {
        CityState city = City(buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 990, endsAt: 1005),
            Idle(2, "hut", 1, 0),
            Upgrading(3, "hut", 2, 0, level: 1, workers: 0, startedAt: 995, endsAt: 1003),
            Idle(4, "lumber_camp", 3, 0, workers: 2),
        ]);

        // Only the idle hut and the upgrading hut count, both at level 1: 3 + 3. The constructing hut and the producer add nothing.
        Assert.Equal(6, CityQueries.WorkersProvided(city, TestContent));
    }

    /// <summary>A level-2 house provides its level's workers (5).</summary>
    [Fact]
    public void WorkersProvided_HouseAtLevelTwo_CountsThatLevelsWorkers()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 0, level: 2)]);

        Assert.Equal(5, CityQueries.WorkersProvided(city, TestContent));
    }

    /// <summary>Assigned workers count on idle, upgrading and producing buildings alike, and free workers are provided minus assigned.</summary>
    [Fact]
    public void WorkersAssignedAndFree_WorkersOnIdleUpgradingAndProducingBuildings_CountEveryAssignment()
    {
        CityState city = City(buildings:
        [
            Idle(1, "hut", 0, 0, level: 2),
            Idle(2, "hut", 1, 0),
            Idle(3, "lumber_camp", 2, 0, workers: 1),
            Upgrading(4, "lumber_camp", 3, 0, level: 1, workers: 2, startedAt: 990, endsAt: 1002),
            Producing(5, "lumber_camp", 4, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010),
        ]);

        Assert.Equal(5, CityQueries.WorkersAssigned(city));
        Assert.Equal(8 - 5, CityQueries.FreeWorkers(city, TestContent));
    }

    /// <summary>Builders are busy for constructions (a site too), upgrades, and upgrades waiting for goods (<c>04</c> section 8).</summary>
    [Fact]
    public void BuildersBusy_EveryPhase_CountsConstructingUpgradingAndWaitingUpgrades()
    {
        CityState city = City(buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 990, endsAt: 1005),
            Upgrading(2, "hut", 1, 0, level: 1, workers: 0, startedAt: 995, endsAt: 1003),
            Idle(3, "hut", 2, 0),
            Producing(4, "lumber_camp", 3, 0, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1030),
            Site(5, "workshop", 4, 0, needs: [("wood", 10)]),
            Idle(6, "hut", 5, 0, needs: [("wood", 5)]),
            Producing(7, "lumber_camp", 0, 1, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1030,
                needs: [("boards", 1)]),
        ]);

        Assert.Equal(5, CityQueries.BuildersBusy(city));
    }

    /// <summary>A building reserves its own running cycle's outputs, a frozen one's too; an idle building reserves nothing (<c>04</c> section 16).</summary>
    [Fact]
    public void Reserved_EachBuilding_IsItsOwnCyclesOutput()
    {
        CityState city = City(buildings:
        [
            Producing(1, "lumber_camp", 0, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010),
            Producing(2, "lumber_camp", 1, 0, 1, workers: 0, cycleStartedAt: 980, cycleLabor: 40, laborRemaining: 20, segmentStartedAt: 990, endsAt: null),
            Idle(3, "lumber_camp", 2, 0, workers: 2),
            Producing(4, "workshop", 3, 0, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 990, endsAt: 1080),
        ]);

        Assert.Equal([1L, 1L, 0L, 0L], city.Buildings.Select(building => CityQueries.Reserved(TestContent, building, _wood)));
        Assert.Equal([0L, 0L, 0L, 1L], city.Buildings.Select(building => CityQueries.Reserved(TestContent, building, _boards)));
        Assert.Equal(0, CityQueries.Reserved(TestContent, city.Buildings[0], _gold));
    }

    /// <summary>Without research a store's effective limit is its level's limit; a good the store doesn't hold has none.</summary>
    [Fact]
    public void TryGetEffectiveLimit_NoResearch_IsTheLevelsLimit()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0), Idle(2, "lumber_camp", 1, 0, level: 2)]);

        Assert.True(CityQueries.TryGetEffectiveLimit(city, TestContent, city.Buildings[0], _wood, out long workshopWood));
        Assert.True(CityQueries.TryGetEffectiveLimit(city, TestContent, city.Buildings[1], _wood, out long campWood));
        Assert.Equal((20L, 150L), (workshopWood, campWood));
        Assert.False(CityQueries.TryGetEffectiveLimit(city, TestContent, city.Buildings[1], _boards, out long none));
        Assert.Equal(0, none);
    }

    /// <summary>A completed storage research adds its bonus to the limit of its good in every store that holds it.</summary>
    [Fact]
    public void TryGetEffectiveLimit_StorageResearchCompleted_AddsTheBonusToEveryStore()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0), Idle(2, "depot", 1, 0)], completedResearch: ["carpentry", "better_saws", "warehouses"]);

        Assert.True(CityQueries.TryGetEffectiveLimit(city, TestContent, city.Buildings[0], _wood, out long workshopWood));
        Assert.True(CityQueries.TryGetEffectiveLimit(city, TestContent, city.Buildings[1], _boards, out long depotBoards));
        Assert.Equal((70L, 140L), (workshopWood, depotBoards));
    }

    /// <summary>The room for a good is limit − stock − the own cycle's output, never below 0 (a refund may put a store above its limit).</summary>
    [Fact]
    public void Room_StoreAndCycle_IsWhatIsLeftUnderTheLimit()
    {
        CityState city = City(buildings:
        [
            Producing(1, "lumber_camp", 0, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010,
                store: [("wood", 60)]),
            Idle(2, "workshop", 1, 0, store: [("wood", 25)]),
        ]);

        Assert.Equal(39, CityQueries.Room(city, TestContent, city.Buildings[0], _wood));
        Assert.Equal(0, CityQueries.Room(city, TestContent, city.Buildings[1], _wood));
        Assert.Equal(80, CityQueries.Room(city, TestContent, city.Buildings[1], _boards));
        Assert.Equal(0, CityQueries.Room(city, TestContent, city.Buildings[0], _boards));
    }

    /// <summary>A city total is the purse for the currency and the sum of every store for a good; <c>Totals</c> lists every item, 0 included.</summary>
    [Fact]
    public void Totals_PurseAndStores_AddUpPerItem()
    {
        CityState city = City(purse: 42, buildings: [Idle(1, "lumber_camp", 0, 0, store: [("wood", 7)]), Idle(2, "depot", 1, 0, store: [("wood", 3)])]);

        Assert.Equal((42L, 10L, 0L), (CityQueries.Total(city, TestContent, _gold), CityQueries.Total(city, TestContent, _wood), CityQueries.Total(city, TestContent, _boards)));
        Assert.Equal([("boards", 0L), ("gold", 42L), ("wood", 10L)], CityQueries.Totals(city, TestContent).Select(total => (total.Item.Value, total.Quantity)));
    }

    /// <summary>Only a completed science building counts; one still constructing or a site doesn't (<c>04</c> section 5).</summary>
    [Fact]
    public void ScienceBuilding_ConstructingOrCompleted_FindsOnlyTheCompletedOne()
    {
        Assert.Null(CityQueries.ScienceBuilding(City(buildings: [Constructing(1, "school", 0, 0, 990, 1005)]), TestContent));
        Assert.Equal(2, CityQueries.ScienceBuilding(City(buildings: [Idle(1, "hut", 0, 0), Idle(2, "school", 1, 0)]), TestContent)?.Id);
    }

    /// <summary>The walk bonus adds up the completed research's walk bonuses; the count of a type counts every phase.</summary>
    [Fact]
    public void WalkBonusAndCountOfType_CompletedResearchAndPhases_AddUp()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 0), Constructing(2, "hut", 1, 0, 990, 1005)], completedResearch: ["boots"]);

        Assert.Equal(100, CityQueries.WalkBonusPermille(city, TestContent));
        Assert.Equal(0, CityQueries.WalkBonusPermille(City(), TestContent));
        Assert.Equal(2, CityQueries.CountOfType(city, new BuildingTypeId("hut")));
        Assert.Equal(0, CityQueries.CountOfType(city, new BuildingTypeId("school")));
    }

    /// <summary>Without research the effective labor is the recipe's.</summary>
    [Fact]
    public void EffectiveLabor_NoResearch_IsTheRecipeLabor()
    {
        Assert.Equal(40, CityQueries.EffectiveLabor(City(), TestContent, new BuildingTypeId("lumber_camp"), 2));
        Assert.Equal(90, CityQueries.EffectiveLabor(City(), TestContent, new BuildingTypeId("workshop"), 1));
    }

    /// <summary>A completed reduction applies per type: the lumber camp's 40 → 30 and the workshop's 90 → 81.</summary>
    [Fact]
    public void EffectiveLabor_ReductionResearchCompleted_AppliesItToEachType()
    {
        CityState city = City(completedResearch: ["carpentry", "better_saws"]);

        // better_saws: lumber camp −250 ‰ (40 → 30), workshop −100 ‰ (90 → 81).
        Assert.Equal(30, CityQueries.EffectiveLabor(city, TestContent, new BuildingTypeId("lumber_camp"), 1));
        Assert.Equal(81, CityQueries.EffectiveLabor(city, TestContent, new BuildingTypeId("workshop"), 1));
    }

    /// <summary>A house has no recipe, an unknown type no definition, and level 3 is outside the workshop's levels: each is a programmer error and throws.</summary>
    [Fact]
    public void EffectiveLabor_HouseOrUnknownType_Throws()
    {
        Assert.Throws<ArgumentException>(() => CityQueries.EffectiveLabor(City(), TestContent, new BuildingTypeId("hut"), 1));
        Assert.Throws<ArgumentException>(() => CityQueries.EffectiveLabor(City(), TestContent, new BuildingTypeId("castle"), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CityQueries.EffectiveLabor(City(), TestContent, new BuildingTypeId("workshop"), 3));
    }

    /// <summary>A type available at the start is unlocked.</summary>
    [Fact]
    public void IsUnlocked_AvailableAtStart_IsTrue()
    {
        Assert.True(CityQueries.IsUnlocked(City(), TestContent, new BuildingTypeId("lumber_camp")));
    }

    /// <summary>A locked type stays locked while its research is not completed, even when it runs.</summary>
    [Fact]
    public void IsUnlocked_UnlockResearchNotCompleted_IsFalse()
    {
        CityState city = City(activeResearch: new ActiveResearch(new ResearchId("carpentry"), 990, 1090));

        Assert.False(CityQueries.IsUnlocked(city, TestContent, new BuildingTypeId("workshop")));
    }

    /// <summary>A locked type is unlocked once its research is completed.</summary>
    [Fact]
    public void IsUnlocked_UnlockResearchCompleted_IsTrue()
    {
        Assert.True(CityQueries.IsUnlocked(City(completedResearch: ["carpentry"]), TestContent, new BuildingTypeId("workshop")));
    }

    /// <summary>A type of a later era is locked until the city reaches that era, even when it is available at the start (OD-10).</summary>
    [Fact]
    public void IsUnlocked_TypeOfALaterEraAvailableAtStart_IsFalseUntilThatEra()
    {
        Assert.False(CityQueries.IsUnlocked(City(era: 1), TestContent, new BuildingTypeId("manor")));
        Assert.True(CityQueries.IsUnlocked(City(era: 2), TestContent, new BuildingTypeId("manor")));
    }

    /// <summary>An unknown type is never unlocked.</summary>
    [Fact]
    public void IsUnlocked_UnknownType_IsFalse()
    {
        Assert.False(CityQueries.IsUnlocked(City(), TestContent, new BuildingTypeId("castle")));
    }

    /// <summary>A city that names a type or research the content doesn't know makes the queries throw: it doesn't belong to this content.</summary>
    [Fact]
    public void Queries_CityNamesWhatTheContentDoesNotKnow_Throw()
    {
        CityState unknownBuilding = City(buildings: [Idle(1, "castle", 0, 0)]);
        CityState unknownResearch = City(completedResearch: ["alchemy"]);

        Assert.Throws<InvalidOperationException>(() => CityQueries.WorkersProvided(unknownBuilding, TestContent));
        Assert.Throws<InvalidOperationException>(() => CityQueries.TryGetEffectiveLimit(
            unknownResearch, TestContent, CityFixture.Idle(1, "workshop", 0, 0), _wood, out _));
        Assert.Throws<InvalidOperationException>(() => CityQueries.EffectiveLabor(unknownResearch, TestContent, new BuildingTypeId("lumber_camp"), 1));
    }
}
