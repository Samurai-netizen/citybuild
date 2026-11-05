using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Small lookups in the game content for the property checks, read only from public state and content, so the checks stay independent of
/// the engine's own queries.
/// </summary>
internal static class ContentLookup
{
    /// <summary>The workers the completed houses provide (a house still constructing provides none).</summary>
    public static long Provided(GameContent content, IEnumerable<Building> buildings) => buildings
        .Where(building => building.Phase != BuildingPhase.Constructing)
        .Sum(building => (long)Definition(content, building.TypeId).GetLevel(building.Level).WorkersProvided);

    /// <summary>The definition of a building type; throws for one the content doesn't know.</summary>
    public static BuildingDefinition Definition(GameContent content, BuildingTypeId type) =>
        content.TryGetBuilding(type, out BuildingDefinition? definition) ? definition : throw new InvalidOperationException($"Unknown type {type}.");
}
