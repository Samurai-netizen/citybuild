#nullable enable
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// How a project starts once its goods are in (<c>core-instructions/04-simulation-model.md</c>, sections 8 and 16): an upgrade at once or when
    /// its last good arrives, used by <see cref="CityRules.UpgradeBuilding"/> and by deliveries.
    /// </summary>
    internal static class ProjectRules
    {
        /// <summary>
        /// Starts a building's upgrade at <paramref name="time"/> (<c>04</c> section 8, starting an upgrade): a running cycle (frozen or not)
        /// ends without output and its inputs go back into the building's own store, even above its limit; then the building is upgrading to
        /// the next level for that level's upgrade seconds. Returns the refunded inputs, or null when no cycle ran.
        /// </summary>
        public static IReadOnlyList<ItemQuantity>? StartUpgrade(GameContent content, Building building, long time)
        {
            IReadOnlyList<ItemQuantity>? refunded = null;
            if (building.Phase == BuildingPhase.Producing)
            {
                // Leaving the Producing phase releases the reservation, which is derived from the phase and never stored.
                refunded = CityQueries.RecipeOf(content, building.TypeId, building.Level).Inputs;
                building.AddToStore(refunded);
            }

            BuildingLevelDefinition next = CityQueries.DefinitionOf(content, building.TypeId).GetLevel(building.Level + 1);
            building.StartUpgrade(time, checked(time + next.UpgradeSeconds));
            return refunded;
        }
    }
}
