using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>
/// Stall reasons (<c>04</c> section 4), checked through their API text (<c>05</c>). Fixture numbers: a workshop makes 1 boards from 2 wood
/// with up to 2 workers; caps are wood 100 and boards 80; the research <c>warehouses</c> adds 40 to the boards cap.
/// </summary>
public sealed class StallReasonTests
{
    /// <summary>An idle house has no recipe: <c>NoRecipe</c>.</summary>
    [Fact]
    public void Of_IdleHouse_IsNoRecipe()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 0)]);

        Assert.Equal("NoRecipe", ReasonOf(city, 1));
    }

    /// <summary>Buildings whose timer makes progress: a constructing house and producer, and an upgrading producer.</summary>
    public static TheoryData<string> BuildingsWithTimers => new() { "constructing hut", "constructing workshop", "upgrading workshop" };

    /// <summary>A constructing or upgrading building makes progress, so it has no stall reason, even without stock or workers.</summary>
    [Theory]
    [MemberData(nameof(BuildingsWithTimers))]
    public void Of_ConstructingOrUpgrading_IsNull(string name)
    {
        Building building = name switch
        {
            "constructing hut" => Constructing(1, "hut", 0, 0, startedAt: 990, endsAt: 1005),
            "constructing workshop" => Constructing(1, "workshop", 0, 0, startedAt: 990, endsAt: 1010),
            _ => Upgrading(1, "workshop", 0, 0, level: 1, workers: 0, startedAt: 990, endsAt: 1005),
        };

        // Nothing in stock and no workers: the building would stall if it were done, but its timer makes progress.
        Assert.Null(ReasonOf(City(buildings: [building]), 1));
    }

    /// <summary>An idle producer without workers: <c>NoWorkers</c>.</summary>
    [Fact]
    public void Of_IdleProducerWithoutWorkers_IsNoWorkers()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0, store: [("wood", 2)])]);

        Assert.Equal("NoWorkers", ReasonOf(city, 1));
    }

    /// <summary>A frozen cycle: <c>NoWorkers</c>.</summary>
    [Fact]
    public void Of_FrozenCycle_IsNoWorkers()
    {
        CityState city = City(buildings:
            [Producing(1, "workshop", 0, 0, 1, workers: 0, cycleStartedAt: 980, cycleLabor: 90, laborRemaining: 50, segmentStartedAt: 990, endsAt: null)]);

        Assert.Equal("NoWorkers", ReasonOf(city, 1));
    }

    /// <summary>A cycle running with workers makes progress, even with the output at its limit and no input left.</summary>
    [Fact]
    public void Of_CycleRunningWithWorkers_IsNull()
    {
        // Boards at the limit and no wood left in its store: the running cycle still makes progress.
        CityState city = City(buildings:
        [
            Producing(1, "workshop", 0, 0, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 990, endsAt: 1080,
                store: [("boards", 80)]),
        ]);

        Assert.Null(ReasonOf(city, 1));
    }

    /// <summary>An idle producer whose own store is short of an input: <c>MissingInput</c> with that item; goods in other buildings don't count.</summary>
    [Fact]
    public void Of_IdleProducerLackingAnInput_IsMissingInputOfThatItem()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0, workers: 1, store: [("wood", 1)]), Idle(2, "depot", 1, 0, store: [("wood", 50)])]);

        Assert.Equal("MissingInput:wood", ReasonOf(city, 1));
    }

    /// <summary>An idle producer whose output is at its store's limit: <c>StorageFull</c> with that item.</summary>
    [Fact]
    public void Of_IdleProducerWhoseOutputIsAtTheLimit_IsStorageFullOfThatItem()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0, workers: 1, store: [("wood", 2), ("boards", 80)])]);

        Assert.Equal("StorageFull:boards", ReasonOf(city, 1));
    }

    /// <summary>
    /// Another building's running cycle doesn't fill this store: a building reserves only its own cycle's outputs (CR-0008: 79 boards and one
    /// more fit 80, whatever the other workshop runs).
    /// </summary>
    [Fact]
    public void Of_AnotherBuildingsCycle_DoesNotCountForThisStore()
    {
        CityState city = City(buildings:
        [
            Producing(1, "workshop", 0, 0, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 990, endsAt: 1080,
                store: [("boards", 79)]),
            Idle(2, "workshop", 1, 0, workers: 1, store: [("wood", 2), ("boards", 79)]),
        ]);

        Assert.Null(ReasonOf(city, 2));
    }

    /// <summary>The check uses the effective limit: with warehouses 80 boards + 1 fits under 120.</summary>
    [Fact]
    public void Of_LimitRaisedByCompletedResearch_UsesTheEffectiveLimit()
    {
        // The level's limit is 80; warehouses adds 40, so 80 boards + 1 fits.
        CityState city = City(
            buildings: [Idle(1, "workshop", 0, 0, workers: 1, store: [("wood", 2), ("boards", 80)])],
            completedResearch: ["carpentry", "better_saws", "warehouses"]);

        Assert.Null(ReasonOf(city, 1));
    }

    /// <summary>An idle producer that could start now has no stall reason.</summary>
    [Fact]
    public void Of_IdleProducerThatCanStart_IsNull()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0, workers: 2, store: [("wood", 2), ("boards", 79)])]);

        Assert.Null(ReasonOf(city, 1));
    }

    /// <summary>
    /// A courier station without couriers is <c>NoWorkers</c> and with them has none; a warehouse and the science building have no recipe
    /// (<c>04</c> section 4, CR-0008).
    /// </summary>
    [Fact]
    public void Of_LogisticsBuildings_HaveTheirReasons()
    {
        CityState city = City(buildings: [Idle(1, "post", 0, 0), Idle(2, "post", 1, 0, workers: 1), Idle(3, "depot", 2, 0), Idle(4, "school", 3, 0)]);

        Assert.Equal(["NoWorkers", null, "NoRecipe", "NoRecipe"], city.Buildings.Select(building => ReasonOf(city, building.Id)));
    }

    /// <summary>The first match wins: no workers before a missing input.</summary>
    [Fact]
    public void Of_NoWorkersAndNoInput_NoWorkersWinsFirst()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0)]);

        Assert.Equal("NoWorkers", ReasonOf(city, 1));
    }

    /// <summary>The first match wins: a missing input before a full output.</summary>
    [Fact]
    public void Of_MissingInputAndFullOutput_MissingInputWinsFirst()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0, workers: 1, store: [("boards", 80)])]);

        Assert.Equal("MissingInput:wood", ReasonOf(city, 1));
    }

    /// <summary>With two inputs missing, the first in ordinal item order is named ("boards" before "wood").</summary>
    [Fact]
    public void Of_TwoInputsMissing_NamesTheFirstInOrdinalOrder()
    {
        // The workshop also needs 1 boards here (its store holds boards); "boards" sorts before "wood". The currency can't be an input (rule 4).
        GameContent content = ContentAssert.Valid(ContentFixture.ValidWith(data =>
            ContentFixture.Level(data, "workshop", 1).Recipe!.Inputs = new Dictionary<string, long?> { ["wood"] = 2, ["boards"] = 1 }));
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0, workers: 1)]);

        Assert.Equal("MissingInput:boards", StallReason.Of(city, content, city.Buildings[0])?.ToString());
    }

    /// <summary>A reason exposes its kind and item; a house's <c>NoRecipe</c> has no item (the default).</summary>
    [Fact]
    public void Of_Reason_HasKindAndItem()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0, workers: 1, store: [("wood", 1)]), Idle(2, "hut", 1, 0)]);

        StallReason missing = StallReason.Of(city, TestContent, city.Buildings[0])!;
        StallReason house = StallReason.Of(city, TestContent, city.Buildings[1])!;

        Assert.Equal((StallKind.MissingInput, new ItemId("wood")), (missing.Kind, missing.Item));
        Assert.Equal((StallKind.NoRecipe, default(ItemId)), (house.Kind, house.Item));
    }

    /// <summary>A null city, content or building throws for that parameter.</summary>
    [Fact]
    public void Of_NullArgument_Throws()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 0)]);

        Assert.Throws<ArgumentNullException>("city", () => StallReason.Of(null!, TestContent, city.Buildings[0]));
        Assert.Throws<ArgumentNullException>("content", () => StallReason.Of(city, null!, city.Buildings[0]));
        Assert.Throws<ArgumentNullException>("building", () => StallReason.Of(city, TestContent, null!));
    }

    /// <summary>The stall reason of the building with the id as text, or null for none.</summary>
    private static string? ReasonOf(CityState city, int buildingId)
    {
        Assert.True(city.TryGetBuilding(buildingId, out Building? building));
        return StallReason.Of(city, TestContent, building)?.ToString();
    }
}
