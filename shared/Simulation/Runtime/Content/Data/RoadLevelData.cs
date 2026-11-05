#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>One road level as read, before any check (see <see cref="RoadLevelDefinition"/> for its meaning; <c>04</c> section 17).</summary>
    public sealed class RoadLevelData
    {
        /// <summary>Required. The level number: 1 for a newly placed road, then 2, 3, … for each upgrade.</summary>
        public int? Level { get; set; }

        /// <summary>Required. The display name, for example <c>Dirt path</c>.</summary>
        public string? Name { get; set; }

        /// <summary>Required. How long a walker takes to cross one tile of this road, in milliseconds.</summary>
        public long? TileMillis { get; set; }

        /// <summary>Required. What one tile costs: to place it at level 1, to upgrade it to this level above, keyed by item id.</summary>
        public Dictionary<string, long?>? Cost { get; set; }
    }
}
