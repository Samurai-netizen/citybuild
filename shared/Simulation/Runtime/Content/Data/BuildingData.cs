#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>A building type as read, before any check (see <see cref="BuildingDefinition"/>).</summary>
    public sealed class BuildingData
    {
        /// <summary>Required. The building type's id text, for example <c>sawmill</c>.</summary>
        public string? Id { get; set; }

        /// <summary>Required. The display name.</summary>
        public string? Name { get; set; }

        /// <summary>Required. <c>house</c> or <c>producer</c>.</summary>
        public string? Kind { get; set; }

        /// <summary>Required. The number of the era the building type belongs to.</summary>
        public int? Era { get; set; }

        /// <summary>
        /// Required. Whether the type can be placed from the start. When false, exactly one research node must unlock it with an
        /// <c>unlockBuilding</c> effect; the unlock itself is stated only there.
        /// </summary>
        public bool? AvailableAtStart { get; set; }

        /// <summary>Required (may be empty). What placing the building costs, keyed by item id.</summary>
        public Dictionary<string, long?>? Cost { get; set; }

        /// <summary>Required. How long construction takes, in seconds.</summary>
        public long? BuildSeconds { get; set; }

        /// <summary>Required. The levels, numbered 1 to the max level, in any order.</summary>
        public List<BuildingLevelData?>? Levels { get; set; }
    }
}
