#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>The bridge as read, before any check (see <see cref="BridgeDefinition"/> for its meaning; <c>04</c> section 17).</summary>
    public sealed class BridgeData
    {
        /// <summary>Required. The display name, for example <c>Bridge</c>.</summary>
        public string? Name { get; set; }

        /// <summary>Required. How long a walker takes to cross one bridge tile, in milliseconds.</summary>
        public long? TileMillis { get; set; }

        /// <summary>Required. What one bridge tile costs to place, keyed by item id.</summary>
        public Dictionary<string, long?>? Cost { get; set; }
    }
}
