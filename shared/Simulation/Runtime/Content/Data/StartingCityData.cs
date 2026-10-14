#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>What a new city starts with, as read, before any check (see <see cref="StartingCityDefinition"/>).</summary>
    public sealed class StartingCityData
    {
        /// <summary>Required. The number of the era the city starts in.</summary>
        public int? Era { get; set; }

        /// <summary>Required. The starting stock, keyed by item id.</summary>
        public Dictionary<string, long?>? Inventory { get; set; }
    }
}
