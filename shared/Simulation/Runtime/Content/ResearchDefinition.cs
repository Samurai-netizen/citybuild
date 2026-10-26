#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// A node of an era's research tree: the player pays its cost up front, waits its duration, and its effects apply when it completes
    /// (<c>core-instructions/04-simulation-model.md</c>, section 5). Research needs no workers.
    /// </summary>
    public sealed class ResearchDefinition
    {
        /// <summary>
        /// Creates the node from values the shape phase has read (<see cref="ContentReader"/>), prerequisites already sorted by id; the rules
        /// check the graph afterwards. Each value is described on its property.
        /// </summary>
        internal ResearchDefinition(
            ResearchId id,
            string name,
            int era,
            IReadOnlyList<ItemQuantity> cost,
            long durationSeconds,
            IReadOnlyList<ResearchId> prerequisites,
            IReadOnlyList<ResearchEffect> effects)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Era = era;
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
            DurationSeconds = durationSeconds;
            Prerequisites = prerequisites ?? throw new ArgumentNullException(nameof(prerequisites));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        /// <summary>The node's id.</summary>
        public ResearchId Id { get; }

        /// <summary>The display name, for example <c>Frame Saw</c>.</summary>
        public string Name { get; }

        /// <summary>The number of the era whose tree the node belongs to. A city can start it only in that era.</summary>
        public int Era { get; }

        /// <summary>What starting the research costs, paid in full at the start, sorted by item id. May be empty.</summary>
        public IReadOnlyList<ItemQuantity> Cost { get; }

        /// <summary>How long the research takes, in seconds (greater than zero). Timers are never capped by the production cap.</summary>
        public long DurationSeconds { get; }

        /// <summary>The nodes that must be completed first, sorted by id. Each is in the same era or an earlier one, and the graph has no cycles.</summary>
        public IReadOnlyList<ResearchId> Prerequisites { get; }

        /// <summary>What completing the node changes, in the order the content lists them. May be empty.</summary>
        public IReadOnlyList<ResearchEffect> Effects { get; }
    }
}
