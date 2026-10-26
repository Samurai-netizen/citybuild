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

    /// <summary>Builders are busy for constructions and upgrades only.</summary>
    [Fact]
    public void BuildersBusy_EveryPhase_CountsConstructingAndUpgrading()
    {
        CityState city = City(buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 990, endsAt: 1005),
            Upgrading(2, "hut", 1, 0, level: 1, workers: 0, startedAt: 995, endsAt: 1003),
            Idle(3, "hut", 2, 0),
            Producing(4, "lumber_camp", 3, 0, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1030),
        ]);

        Assert.Equal(2, CityQueries.BuildersBusy(city));
    }

    /// <summary>The reserved quantity sums the outputs of every producing building, frozen cycles included.</summary>
    [Fact]
    public void Reserved_ProducingBuildingsFrozenIncluded_SumsTheirOutputs()
    {
        CityState city = City(buildings:
        [
            Producing(1, "lumber_camp", 0, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010),
            Producing(2, "lumber_camp", 1, 0, 1, workers: 0, cycleStartedAt: 980, cycleLabor: 40, laborRemaining: 20, segmentStartedAt: 990, endsAt: null),
            Idle(3, "lumber_camp", 2, 0, workers: 2),
            Producing(4, "workshop", 3, 0, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 990, endsAt: 1080),
        ]);

        Assert.Equal(2, CityQueries.Reserved(city, TestContent, _wood));
        Assert.Equal(1, CityQueries.Reserved(city, TestContent, _boards));
        Assert.Equal(0, CityQueries.Reserved(city, TestContent, _gold));
    }

    /// <summary>Without research the effective cap is the base cap.</summary>
    [Fact]
    public void TryGetEffectiveCap_NoResearch_IsTheBaseCap()
    {
        Assert.True(CityQueries.TryGetEffectiveCap(City(), TestContent, _wood, out long cap));
        Assert.Equal(100, cap);
    }

    /// <summary>A completed storage research adds its bonus to the base cap.</summary>
    [Fact]
    public void TryGetEffectiveCap_StorageResearchCompleted_AddsTheBonus()
    {
        CityState city = City(completedResearch: ["carpentry", "better_saws", "warehouses"]);

        Assert.True(CityQueries.TryGetEffectiveCap(city, TestContent, _wood, out long woodCap));
        Assert.True(CityQueries.TryGetEffectiveCap(city, TestContent, _boards, out long boardsCap));
        Assert.Equal((150L, 120L), (woodCap, boardsCap));
    }

    /// <summary>An uncapped item has no cap at all.</summary>
    [Fact]
    public void TryGetEffectiveCap_UncappedItem_ReturnsFalse()
    {
        Assert.False(CityQueries.TryGetEffectiveCap(City(completedResearch: ["warehouses"]), TestContent, _gold, out long cap));
        Assert.Equal(0, cap);
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
        Assert.Throws<InvalidOperationException>(() => CityQueries.TryGetEffectiveCap(unknownResearch, TestContent, _wood, out _));
        Assert.Throws<InvalidOperationException>(() => CityQueries.EffectiveLabor(unknownResearch, TestContent, new BuildingTypeId("lumber_camp"), 1));
    }
}
