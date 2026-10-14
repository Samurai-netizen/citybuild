#nullable enable
using System;

namespace CityBuilder.Simulation.State
{
    /// <summary>The research a city is running in its one research slot (<c>core-instructions/04-simulation-model.md</c>, section 5). Immutable.</summary>
    public sealed class ActiveResearch
    {
        /// <summary>Creates the running research, for example when restoring a city from the database.</summary>
        /// <param name="id">The research node.</param>
        /// <param name="startedAt">When it started, in city-clock seconds, at least 0.</param>
        /// <param name="endsAt">When it completes, in city-clock seconds, not before <paramref name="startedAt"/>.</param>
        /// <exception cref="ArgumentException">The start is negative or the end is before it.</exception>
        public ActiveResearch(ResearchId id, long startedAt, long endsAt)
        {
            if (startedAt < 0 || endsAt < startedAt)
            {
                throw new ArgumentException(FormattableString.Invariant($"Research {id}: the start must not be negative and the end not before it."));
            }

            Id = id;
            StartedAt = startedAt;
            EndsAt = endsAt;
        }

        /// <summary>The research node.</summary>
        public ResearchId Id { get; }

        /// <summary>When it started, in city-clock seconds. Its cost was paid at that moment.</summary>
        public long StartedAt { get; }

        /// <summary>When it completes and its effects apply, in city-clock seconds: the start plus the node's duration.</summary>
        public long EndsAt { get; }
    }
}
