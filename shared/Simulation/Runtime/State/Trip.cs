#nullable enable
using System;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// A manual trip (<c>core-instructions/04-simulation-model.md</c>, section 18): one free worker carries goods of one item from a building's
    /// store to another building, walking by the path rule (section 17). The goods left the source's store when the trip started; at
    /// <see cref="ArrivesAt"/> they are delivered (a waiting project first, then the target's store) and what doesn't fit goes back to the
    /// source. Until then the worker is busy. A trip never changes: it is a value the city holds until it arrives.
    /// </summary>
    public sealed class Trip
    {
        /// <summary>Creates a trip, for example when restoring a city from the database.</summary>
        /// <param name="id">The trip's id in its city, at least 1.</param>
        /// <param name="sourceId">The building the goods came from, at least 1.</param>
        /// <param name="targetId">The building they go to, at least 1, not the source.</param>
        /// <param name="item">The good carried (never the currency; the rules check that).</param>
        /// <param name="quantity">How many, at least 1.</param>
        /// <param name="departedAt">When the worker left, in city-clock seconds, at least 0.</param>
        /// <param name="arrivesAt">When the worker arrives, after <paramref name="departedAt"/>.</param>
        /// <exception cref="ArgumentException">The values don't form a trip.</exception>
        public Trip(int id, int sourceId, int targetId, ItemId item, long quantity, long departedAt, long arrivesAt)
        {
            if (id < 1 || sourceId < 1 || targetId < 1 || sourceId == targetId || quantity < 1 || departedAt < 0 || arrivesAt <= departedAt)
            {
                throw new ArgumentException(FormattableString.Invariant(
                    $"Trip {id}: ids at least 1, the source not the target, a quantity of at least 1, and an arrival after a departure not below 0."));
            }

            Id = id;
            SourceId = sourceId;
            TargetId = targetId;
            Item = item;
            Quantity = quantity;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
        }

        /// <summary>The trip's id in its city: unique, never reused, given in start order from 1.</summary>
        public int Id { get; }

        /// <summary>The building whose store the goods came from; what doesn't fit at the target goes back there.</summary>
        public int SourceId { get; }

        /// <summary>The building the goods are delivered to.</summary>
        public int TargetId { get; }

        /// <summary>The good carried.</summary>
        public ItemId Item { get; }

        /// <summary>How many the worker carries.</summary>
        public long Quantity { get; }

        /// <summary>When the worker left the source, in city-clock seconds.</summary>
        public long DepartedAt { get; }

        /// <summary>When the worker arrives at the target and the goods are delivered, in city-clock seconds.</summary>
        public long ArrivesAt { get; }
    }
}
