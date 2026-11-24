using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// Placing a building on the Era I map (<c>04</c> section 8, <c>29</c> → Map, M1-03): a Woodcutter's Hut needs forest beside it, a Quarry
/// rock, a Farm fertile soil; nothing stands on water, forest or rock; nothing stands on land still under mist, and <c>openLand</c>
/// research opens its area at its completion second. Tiles are those of the map in <c>29</c>: area A is x 2–12, y 2–11, the river x = 12.
/// </summary>
public sealed class TerrainPlacementTests
{
    /// <summary>The server's Era I content, with the map.</summary>
    private static readonly GameContent _content = ContentAssert.Valid(ContentFile.ReadServer("era1-content.json"));

    /// <summary>A Woodcutter's Hut on grass with forest beside it (8, 9) is placed.</summary>
    [Fact]
    public void PlaceBuilding_WoodcutterBesideForest_IsPlaced() => RulesAssert.Ok(Place(Rich(), "woodcutter", 7, 9));

    /// <summary>A Woodcutter's Hut with no forest in the four directions is refused.</summary>
    [Fact]
    public void PlaceBuilding_WoodcutterWithoutForestBeside_IsNeedsForest() =>
        RulesAssert.Rejected(MutationErrorCode.NeedsForest, Rich(), city => Place(city, "woodcutter", 5, 5));

    /// <summary>A forest only diagonally beside the tile is not beside it: (7, 8) touches (8, 9) at a corner only.</summary>
    [Fact]
    public void PlaceBuilding_WoodcutterWithForestOnlyDiagonally_IsNeedsForest() =>
        RulesAssert.Rejected(MutationErrorCode.NeedsForest, Rich(), city => Place(city, "woodcutter", 7, 8));

    /// <summary>A forest beside the tile may lie under mist: (2, 7) has forest at (1, 7), in the closed area B.</summary>
    [Fact]
    public void PlaceBuilding_WoodcutterBesideForestUnderMist_IsPlaced() => RulesAssert.Ok(Place(Rich(), "woodcutter", 2, 7));

    /// <summary>A Quarry beside rock (9, 3) is placed.</summary>
    [Fact]
    public void PlaceBuilding_QuarryBesideRock_IsPlaced() => RulesAssert.Ok(Place(Rich("stone_cutting"), "quarry", 8, 3));

    /// <summary>A Quarry with no rock beside it is refused.</summary>
    [Fact]
    public void PlaceBuilding_QuarryWithoutRockBeside_IsNeedsRock() =>
        RulesAssert.Rejected(MutationErrorCode.NeedsRock, Rich("stone_cutting"), city => Place(city, "quarry", 5, 5));

    /// <summary>A Farm on fertile soil of the opened area C is placed.</summary>
    [Fact]
    public void PlaceBuilding_FarmOnFertileSoil_IsPlaced() => RulesAssert.Ok(Place(Rich("open_fields"), "farm", 13, 3));

    /// <summary>A Farm on grass, even in area C, is refused.</summary>
    [Fact]
    public void PlaceBuilding_FarmOffFertileSoil_IsNeedsFertileSoil() =>
        RulesAssert.Rejected(MutationErrorCode.NeedsFertileSoil, Rich("open_fields"), city => Place(city, "farm", 14, 11));

    /// <summary>Any other type stands on fertile soil too.</summary>
    [Fact]
    public void PlaceBuilding_HouseOnFertileSoil_IsPlaced() => RulesAssert.Ok(Place(Rich("open_fields"), "house", 14, 3));

    /// <summary>Nothing stands on water, forest or rock: each is refused as blocked, whatever the type needs.</summary>
    [Theory]
    [InlineData("house", 12, 5)]
    [InlineData("house", 8, 9)]
    [InlineData("house", 9, 3)]
    [InlineData("woodcutter", 8, 9)]
    [InlineData("woodcutter", 12, 9)]
    public void PlaceBuilding_OnWaterForestOrRock_IsTerrainBlocked(string type, int x, int y) =>
        RulesAssert.Rejected(MutationErrorCode.TerrainBlocked, Rich(), city => Place(city, type, x, y));

    /// <summary>Grass under mist (area B, not yet surveyed) is refused as closed land.</summary>
    [Fact]
    public void PlaceBuilding_OnLandUnderMist_IsLandClosed() =>
        RulesAssert.Rejected(MutationErrorCode.LandClosed, Rich(), city => Place(city, "house", 6, 0));

    /// <summary>Blocked terrain is reported before closed land: the terrain never changes, the land opens (OD-13 can refuse it first).</summary>
    [Fact]
    public void PlaceBuilding_OnForestUnderMist_IsTerrainBlocked() =>
        RulesAssert.Rejected(MutationErrorCode.TerrainBlocked, Rich(), city => Place(city, "house", 0, 0));

    /// <summary>A site that lacks its terrain is reported before closed land, for the same reason.</summary>
    [Fact]
    public void PlaceBuilding_FarmOnGrassUnderMist_IsNeedsFertileSoil() =>
        RulesAssert.Rejected(MutationErrorCode.NeedsFertileSoil, Rich("bridge_building", "open_fields"), city => Place(city, "farm", 6, 0));

    /// <summary>Once <c>land_survey</c> is done, area B is open.</summary>
    [Fact]
    public void PlaceBuilding_OnSurveyedLand_IsPlaced() => RulesAssert.Ok(Place(Rich("land_survey"), "house", 6, 0));

    /// <summary>
    /// The land opens at the research's completion second: one second before it the tile is still closed, at it the house is placed.
    /// </summary>
    [Fact]
    public void PlaceBuilding_AtTheSurveysCompletionSecond_OpensTheLand()
    {
        CityState city = City(clock: 1000, purse: 100_000, activeResearch: new ActiveResearch(new ResearchId("land_survey"), 1000, 1100));

        Simulator.Advance(city, _content, 99);
        RulesAssert.Rejected(MutationErrorCode.LandClosed, city, state => Place(state, "house", 6, 0));
        Simulator.Advance(city, _content, 1);

        RulesAssert.Ok(Place(city, "house", 6, 0));
    }

    /// <summary>The open land of a city: area A from the start, area B after <c>land_survey</c>, C after <c>open_fields</c>.</summary>
    [Fact]
    public void IsLandOpen_ByResearch_OpensEachArea()
    {
        CityState start = Rich();
        CityState surveyed = Rich("land_survey");

        Assert.Equal((true, false, false), (CityQueries.IsLandOpen(start, _content, 5, 5), CityQueries.IsLandOpen(start, _content, 6, 0), CityQueries.IsLandOpen(start, _content, 13, 3)));
        Assert.Equal((true, true, false), (CityQueries.IsLandOpen(surveyed, _content, 5, 5), CityQueries.IsLandOpen(surveyed, _content, 6, 0), CityQueries.IsLandOpen(surveyed, _content, 13, 3)));
        Assert.True(CityQueries.IsLandOpen(Rich("open_fields"), _content, 13, 3));
    }

    /// <summary>Content without a map: every tile is open.</summary>
    [Fact]
    public void IsLandOpen_ContentWithoutMap_IsAlwaysOpen() =>
        Assert.True(CityQueries.IsLandOpen(City(), ContentAssert.Valid(ContentFixture.Valid()), 5, 4));

    /// <summary>A city with coins for anything and the research named done (the unlocks a type needs, and the land it opens).</summary>
    private static CityState Rich(params string[] research) => City(purse: 100_000, completedResearch: research);

    /// <summary>Places a building of the Era I content.</summary>
    private static MutationResult Place(CityState city, string type, int x, int y) =>
        CityRules.PlaceBuilding(city, _content, new BuildingTypeId(type), x, y);
}
