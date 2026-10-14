#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>A recipe as read, before any check (see <see cref="RecipeDefinition"/>).</summary>
    public sealed class RecipeData
    {
        /// <summary>Optional, none when left out. What one cycle consumes at its start, keyed by item id.</summary>
        public Dictionary<string, long?>? Inputs { get; set; }

        /// <summary>Required, at least one. What one cycle makes at its end, keyed by item id.</summary>
        public Dictionary<string, long?>? Outputs { get; set; }

        /// <summary>Required. The work one cycle needs, in worker-seconds.</summary>
        public long? Labor { get; set; }
    }
}
