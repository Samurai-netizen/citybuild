#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>What a building type does. The set is closed; a new kind is an engine change (<c>core-instructions/19-change-recipes.md</c>).</summary>
    public enum BuildingKind
    {
        /// <summary>Provides workers once completed (per level); has no recipe and takes no workers. JSON: <c>house</c>.</summary>
        House,

        /// <summary>Runs a recipe with assigned workers (at most its level's max workers). JSON: <c>producer</c>.</summary>
        Producer,
    }
}
