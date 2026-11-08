#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// The research a city runs in its one research slot (<c>core-instructions/04-simulation-model.md</c>, sections 5 and 16). Its coins were paid
    /// when it was started; while goods it needs are missing it <b>waits</b> (no start, no end) for deliveries to the science building, and its
    /// timer runs from the second the last of them arrives. Only the engine changes it.
    /// </summary>
    public sealed class ActiveResearch
    {
        /// <summary>The goods the research still needs, only quantities above 0, sorted by item id; empty once its timer runs.</summary>
        private readonly ItemTotals _needs = new ItemTotals();

        /// <summary>Creates a research whose timer runs, for example when restoring a city from the database.</summary>
        /// <param name="id">The research node.</param>
        /// <param name="startedAt">When its timer started, in city-clock seconds, at least 0.</param>
        /// <param name="endsAt">When it completes, in city-clock seconds, not before <paramref name="startedAt"/>.</param>
        /// <exception cref="ArgumentException">The start is negative or the end is before it.</exception>
        public ActiveResearch(ResearchId id, long startedAt, long endsAt)
            : this(id, startedAt, endsAt, null)
        {
        }

        /// <summary>
        /// Creates the running research with every value: either its timer runs (a start and an end, no needs) or it waits for goods (needs,
        /// no start and no end).
        /// </summary>
        /// <param name="id">The research node.</param>
        /// <param name="startedAt">When its timer started, at least 0; null while it waits.</param>
        /// <param name="endsAt">When it completes, not before the start; null while it waits.</param>
        /// <param name="needs">The goods it still needs, each at most once, none below 0 (0 is left out); null or empty when its timer runs.</param>
        /// <exception cref="ArgumentException">The values are neither a running nor a waiting research.</exception>
        public ActiveResearch(ResearchId id, long? startedAt, long? endsAt, IEnumerable<KeyValuePair<ItemId, long>>? needs)
        {
            Id = id;
            var seen = new HashSet<ItemId>();
            foreach (KeyValuePair<ItemId, long> need in needs ?? Array.Empty<KeyValuePair<ItemId, long>>())
            {
                if (need.Value < 0 || !seen.Add(need.Key))
                {
                    throw new ArgumentException(FormattableString.Invariant($"Research {id}: its needs list {need.Key} twice or below 0."));
                }

                _needs.Set(need.Key, need.Value);
            }

            bool running = startedAt >= 0 && endsAt >= startedAt && _needs.Count == 0;
            bool waiting = !startedAt.HasValue && !endsAt.HasValue && _needs.Count > 0;
            if (!running && !waiting)
            {
                throw new ArgumentException(FormattableString.Invariant(
                    $"Research {id}: either a start not below 0 and an end not before it, or (waiting for goods) needs and no start or end."));
            }

            StartedAt = startedAt;
            EndsAt = endsAt;
            Needs = _needs;
        }

        /// <summary>The research node.</summary>
        public ResearchId Id { get; }

        /// <summary>When its timer started, in city-clock seconds; null while it waits for goods.</summary>
        public long? StartedAt { get; private set; }

        /// <summary>When it completes and its effects apply, in city-clock seconds: its timer's start plus the node's duration; null while it waits.</summary>
        public long? EndsAt { get; private set; }

        /// <summary>The goods it still needs at the science building, by item id; empty once its timer runs.</summary>
        public IReadOnlyDictionary<ItemId, long> Needs { get; }

        /// <summary>Whether it waits for goods (its timer doesn't run yet).</summary>
        public bool IsWaiting => _needs.Count > 0;

        /// <summary>A research that waits for <paramref name="needs"/> (at least one good above 0) after its coins were paid.</summary>
        internal static ActiveResearch Waiting(ResearchId id, IReadOnlyList<ItemQuantity> needs)
        {
            var pairs = new List<KeyValuePair<ItemId, long>>(needs.Count);
            for (int index = 0; index < needs.Count; index++)
            {
                pairs.Add(new KeyValuePair<ItemId, long>(needs[index].Item, needs[index].Quantity));
            }

            return new ActiveResearch(id, null, null, pairs);
        }

        /// <summary>Hands up to <paramref name="quantity"/> of a good to the research: at most what it still needs. Returns what it took.</summary>
        internal long FillNeed(ItemId item, long quantity)
        {
            long taken = Math.Min(Math.Max(quantity, 0), _needs.Get(item));
            _needs.Set(item, checked(_needs.Get(item) - taken));
            return taken;
        }

        /// <summary>Its needs are in: the timer runs from <paramref name="startedAt"/> to <paramref name="endsAt"/>.</summary>
        internal void StartTimer(long startedAt, long endsAt)
        {
            if (_needs.Count > 0 || EndsAt.HasValue || endsAt < startedAt)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Research {Id}: only a research whose goods are in starts its timer."));
            }

            StartedAt = startedAt;
            EndsAt = endsAt;
        }
    }
}
