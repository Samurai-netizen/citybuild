#nullable enable
using System;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// One line of a courier's delivery queue (<c>core-instructions/04-simulation-model.md</c>, sections 18 and 19, CR-0008 D16): "take at the
    /// source, put at the target", of one good, at most the quantity. A delivery the player sends becomes one or more lines of at most the
    /// courier's carry limit; the goods stay in the source's store until the courier picks them up. A courier runs its lines in id order; a
    /// line never changes and leaves the city when it ends or is cancelled.
    /// </summary>
    public sealed class DeliveryLine
    {
        /// <summary>Creates a line, for example when restoring a city from the database.</summary>
        /// <param name="id">The line's id in its city, at least 1.</param>
        /// <param name="stationId">The courier station of the courier that runs it, at least 1.</param>
        /// <param name="slot">That courier's slot, at least 1.</param>
        /// <param name="sourceId">The building the goods are taken from, at least 1.</param>
        /// <param name="targetId">The building they are put into, at least 1, not the source.</param>
        /// <param name="item">The good (never the currency; the rules check that).</param>
        /// <param name="quantity">The most the line moves, at least 1.</param>
        /// <exception cref="ArgumentException">The values don't form a line.</exception>
        public DeliveryLine(int id, int stationId, int slot, int sourceId, int targetId, ItemId item, long quantity)
        {
            if (id < 1 || stationId < 1 || slot < 1 || sourceId < 1 || targetId < 1 || sourceId == targetId || quantity < 1)
            {
                throw new ArgumentException(FormattableString.Invariant(
                    $"Delivery line {id}: ids and the slot at least 1, the source not the target, and a quantity of at least 1."));
            }

            Id = id;
            StationId = stationId;
            Slot = slot;
            SourceId = sourceId;
            TargetId = targetId;
            Item = item;
            Quantity = quantity;
        }

        /// <summary>The line's id in its city: unique, never reused, given in send order from 1; a courier runs its lines in this order.</summary>
        public int Id { get; }

        /// <summary>The courier station whose courier runs the line.</summary>
        public int StationId { get; }

        /// <summary>The slot of that courier at its station.</summary>
        public int Slot { get; }

        /// <summary>The building whose store the goods are taken from.</summary>
        public int SourceId { get; }

        /// <summary>The building the goods are put into (a waiting project first, <c>04</c> section 16).</summary>
        public int TargetId { get; }

        /// <summary>The good moved.</summary>
        public ItemId Item { get; }

        /// <summary>The most the line moves; at most the carry limit of its station's level when it was sent.</summary>
        public long Quantity { get; }
    }
}
