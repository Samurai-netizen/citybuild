#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>What a building type does. The set is closed; a new kind is an engine change (<c>core-instructions/19-change-recipes.md</c>).</summary>
    public enum BuildingKind
    {
        /// <summary>Provides workers once completed (per level); has no recipe and takes no workers. JSON: <c>house</c>.</summary>
        House,

        /// <summary>Runs a recipe with assigned workers (at most its level's max workers) on the goods of its own store. JSON: <c>producer</c>.</summary>
        Producer,

        /// <summary>Stores goods up to a limit per good, to collect them for building work and to buffer chains (CR-0008 D13). JSON: <c>warehouse</c>.</summary>
        Warehouse,

        /// <summary>Its workers are its couriers, one per worker; its level sets their carry limit and stamina (<c>04</c> section 19). JSON: <c>courierStation</c>.</summary>
        CourierStation,

        /// <summary>Where research happens, one per city; a research's goods are delivered to it (<c>04</c> section 16, CR-0008 D11). JSON: <c>science</c>.</summary>
        Science,
    }
}
