#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>A research node as read, before any check (see <see cref="ResearchDefinition"/>).</summary>
    public sealed class ResearchData
    {
        /// <summary>Required. The node's id text, for example <c>frame_saw</c>.</summary>
        public string? Id { get; set; }

        /// <summary>Required. The display name.</summary>
        public string? Name { get; set; }

        /// <summary>Required. The number of the era whose tree the node belongs to.</summary>
        public int? Era { get; set; }

        /// <summary>Required (may be empty). What starting the research costs, keyed by item id.</summary>
        public Dictionary<string, long?>? Cost { get; set; }

        /// <summary>Required. How long the research takes, in seconds.</summary>
        public long? DurationSeconds { get; set; }

        /// <summary>Optional, none when left out. The ids of the nodes that must be completed first.</summary>
        public List<string?>? Prerequisites { get; set; }

        /// <summary>Optional, none when left out. What completing the node changes.</summary>
        public List<ResearchEffectData?>? Effects { get; set; }
    }
}
