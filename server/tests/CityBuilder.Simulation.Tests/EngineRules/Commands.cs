using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// A player's commands as the server runs them on a city it has settled to now (<c>04</c> sections 7 and 8), over
/// <see cref="CityFixture.TestContent"/>: the mutation must succeed, and a zero-length advance follows, so a cycle that can start at the
/// clock has started when the command returns.
/// </summary>
internal static class Commands
{
    /// <summary>Places a building of the type on the tile, asserting success, then runs the zero-length advance.</summary>
    public static void PlaceBuilding(CityState city, string type, int x, int y) =>
        Run(city, target => CityRules.PlaceBuilding(target, TestContent, new BuildingTypeId(type), x, y));

    /// <summary>Starts the upgrade of a building, asserting success, then runs the zero-length advance.</summary>
    public static void UpgradeBuilding(CityState city, int buildingId) => Run(city, target => CityRules.UpgradeBuilding(target, TestContent, buildingId));

    /// <summary>Sets a building's workers to an absolute count, asserting success, then runs the zero-length advance.</summary>
    public static void AssignWorkers(CityState city, int buildingId, int workers) =>
        Run(city, target => CityRules.AssignWorkers(target, TestContent, buildingId, workers));

    /// <summary>Starts a research node, asserting success, then runs the zero-length advance.</summary>
    public static void StartResearch(CityState city, string researchId) =>
        Run(city, target => CityRules.StartResearch(target, TestContent, new ResearchId(researchId)));

    /// <summary>Applies a mutation, asserts it was applied, and ends with the zero-length advance every city request ends with (<c>04</c> section 7).</summary>
    private static void Run(CityState city, Func<CityState, MutationResult> mutation)
    {
        RulesAssert.Ok(mutation(city));
        Simulator.Advance(city, TestContent, 0);
    }
}
