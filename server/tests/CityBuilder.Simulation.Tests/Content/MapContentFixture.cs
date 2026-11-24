using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The rule fixture (<see cref="ContentFixture"/>) on a 6 × 5 map (M1-03). Terrain, one row per y:
/// <code>
/// F....W   areas  AAAABB
/// .....W          AAAABB
/// R....W          AAAABB
/// .....W          AAAABB
/// SS...W          CCCCBB
/// </code>
/// Area A is open at the start; tallying opens B, charter opens C, carpentry unlocks the bridge (30 gold, 1 500 ms); forest and rock take
/// 9 000 ms to cross; a lumber camp needs forest beside it.
/// </summary>
internal static class MapContentFixture
{
    /// <summary>The rule fixture with the map, the bridge, the lumber camp's site and the three effects.</summary>
    public static GameContentData Valid()
    {
        GameContentData data = ContentFixture.Valid();
        data.Map = new MapData
        {
            Id = "meadow",
            StartArea = "A",
            RoughTileMillis = 9000,
            Tiles = ["F....W", ".....W", "R....W", ".....W", "SS...W"],
            Areas = ["AAAABB", "AAAABB", "AAAABB", "AAAABB", "CCCCBB"],
        };
        data.Bridge = new BridgeData { Name = "Bridge", TileMillis = 1500, Cost = new() { ["gold"] = 30 } };
        Building(data, "lumber_camp").Site = "nearForest";
        Research(data, "tallying").Effects!.Add(new ResearchEffectData { Type = "openLand", Area = "B" });
        Research(data, "charter").Effects!.Add(new ResearchEffectData { Type = "openLand", Area = "C" });
        Research(data, "carpentry").Effects!.Add(new ResearchEffectData { Type = "unlockBridge" });
        return data;
    }

    /// <summary>The map fixture with one change applied.</summary>
    public static GameContentData ValidWith(Action<GameContentData> change)
    {
        GameContentData data = Valid();
        change(data);
        return data;
    }
}
