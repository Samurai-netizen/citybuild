#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>Makes a building type placeable. Each type that is not available at the start is unlocked by exactly one research node. JSON: <c>unlockBuilding</c>.</summary>
    public sealed class UnlockBuildingEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from an <c>unlockBuilding</c> entry; the rules check that the type exists and is unlocked only here.</summary>
        internal UnlockBuildingEffect(BuildingTypeId building)
        {
            Building = building;
        }

        /// <summary>The building type that becomes placeable.</summary>
        public BuildingTypeId Building { get; }
    }
}
