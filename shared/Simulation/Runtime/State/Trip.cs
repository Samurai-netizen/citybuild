#nullable enable
using System;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// A worker's delivery (<c>core-instructions/04-simulation-model.md</c>, section 18, CR-0008 D21), sent while the city has no built courier
    /// station: one free worker shuttles goods of one item from a building's store to another building, walking by the path rule (section 17),
    /// one load of at most the trip carry limit at a time. On a <see cref="TripLeg.ToTarget"/> leg it carries <see cref="Quantity"/>, which left
    /// the source's store when the leg began; at <see cref="ArrivesAt"/> it is delivered (a waiting project first, then the target's store).
    /// While <see cref="Remaining"/> is above 0 the worker then walks back (<see cref="TripLeg.ToSource"/>) for the next load. What doesn't fit
    /// at the target ends the sending: the worker carries it back (<see cref="TripLeg.ToSource"/> with a load and nothing remaining), puts back
    /// what fits and waits at the source (<see cref="TripLeg.Waiting"/>) until the rest fits (M0-C-3). Until the trip ends the worker is busy.
    /// </summary>
    public sealed class Trip
    {
        /// <summary>Creates a trip, for example when restoring a city from the database.</summary>
        /// <param name="id">The trip's id in its city, at least 1.</param>
        /// <param name="sourceId">The building the goods come from, at least 1.</param>
        /// <param name="targetId">The building they go to, at least 1, not the source.</param>
        /// <param name="item">The good carried (never the currency; the rules check that).</param>
        /// <param name="quantity">How many the worker carries now: at least 1 on the way to the target and while waiting; on the way back 0
        /// (with some remaining) or the goods the target didn't take (with none remaining).</param>
        /// <param name="departedAt">When the worker left for this leg, in city-clock seconds, at least 0.</param>
        /// <param name="arrivesAt">When the leg ends, after <paramref name="departedAt"/>.</param>
        /// <param name="remaining">How many of the delivery are still in the source's store, to be fetched by later loads, at least 0.</param>
        /// <param name="leg">Which way the worker walks now.</param>
        /// <exception cref="ArgumentException">The values don't form a trip.</exception>
        public Trip(int id, int sourceId, int targetId, ItemId item, long quantity, long departedAt, long arrivesAt, long remaining = 0, TripLeg leg = TripLeg.ToTarget)
        {
            bool loadFits = leg switch
            {
                TripLeg.ToTarget => quantity >= 1,
                TripLeg.ToSource => (quantity == 0 && remaining >= 1) || (quantity >= 1 && remaining == 0),
                TripLeg.Waiting => quantity >= 1 && remaining == 0,
                _ => false,
            };
            if (id < 1 || sourceId < 1 || targetId < 1 || sourceId == targetId || !loadFits || remaining < 0 || departedAt < 0 || arrivesAt <= departedAt)
            {
                throw new ArgumentException(FormattableString.Invariant(
                    $"Trip {id}: ids at least 1, the source not the target, a load of at least 1 to the target or while waiting (nothing remaining), back to the source either none with some remaining or a load with none remaining, nothing negative, and an arrival after a departure not below 0."));
            }

            Id = id;
            SourceId = sourceId;
            TargetId = targetId;
            Item = item;
            Quantity = quantity;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
            Remaining = remaining;
            Leg = leg;
        }

        /// <summary>The trip's id in its city: unique, never reused, given in start order from 1.</summary>
        public int Id { get; }

        /// <summary>The building whose store the goods come from; what doesn't fit at the target goes back there.</summary>
        public int SourceId { get; }

        /// <summary>The building the goods are delivered to.</summary>
        public int TargetId { get; }

        /// <summary>The good carried.</summary>
        public ItemId Item { get; }

        /// <summary>How many the worker carries now: the load to the target, the goods it carries back or waits with, 0 on an empty way back.</summary>
        public long Quantity { get; private set; }

        /// <summary>When the worker began the current leg, in city-clock seconds.</summary>
        public long DepartedAt { get; private set; }

        /// <summary>When the current leg ends, in city-clock seconds: the delivery at the target, or the next pickup at the source.</summary>
        public long ArrivesAt { get; private set; }

        /// <summary>How many of the delivery are still to be fetched from the source by later loads (they are still in its store).</summary>
        public long Remaining { get; private set; }

        /// <summary>Which way the worker walks now.</summary>
        public TripLeg Leg { get; private set; }

        /// <summary>After a delivery at the target: the worker walks back to the source, empty, for the next load.</summary>
        internal void WalkBack(long departedAt, long arrivesAt)
        {
            Quantity = 0;
            Leg = TripLeg.ToSource;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
        }

        /// <summary>
        /// After a delivery at the target that left <paramref name="load"/> (at least 1): the sending ends (nothing more is fetched) and the
        /// worker walks the rest back to the source.
        /// </summary>
        internal void CarryBack(long load, long departedAt, long arrivesAt)
        {
            Quantity = load;
            Remaining = 0;
            Leg = TripLeg.ToSource;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
        }

        /// <summary>At the source with goods its store can't take yet: the worker waits there; the times stay those of the walk back.</summary>
        internal void Wait()
        {
            Leg = TripLeg.Waiting;
        }

        /// <summary>Takes <paramref name="quantity"/> (at least 1, at most <see cref="Quantity"/>) off the load that goes back into the source.</summary>
        internal void Unload(long quantity)
        {
            if (quantity < 1 || quantity > Quantity)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Trip {Id}: can't unload {quantity} of a load of {Quantity}."));
            }

            Quantity -= quantity;
        }

        /// <summary>At the source: the worker took <paramref name="load"/> (at least 1, at most <see cref="Remaining"/>) and walks to the target.</summary>
        internal void CarryOn(long load, long departedAt, long arrivesAt)
        {
            Quantity = load;
            Remaining = checked(Remaining - load);
            Leg = TripLeg.ToTarget;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
        }
    }
}
