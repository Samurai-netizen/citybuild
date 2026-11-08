#nullable enable
using System;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// One part of what a courier carries (<c>core-instructions/04-simulation-model.md</c>, section 19): a quantity of a good and the building it
    /// was taken from, so a courier that stops working can give each part back to where it came from. A value; the courier replaces a part
    /// when its quantity changes.
    /// </summary>
    public sealed class CourierLoad
    {
        /// <summary>Creates a load part.</summary>
        /// <param name="item">The good (never the currency; the rules check that).</param>
        /// <param name="fromId">The building it was taken from, at least 1.</param>
        /// <param name="quantity">How many, at least 1.</param>
        /// <exception cref="ArgumentException">The building id or the quantity is below 1.</exception>
        public CourierLoad(ItemId item, int fromId, long quantity)
        {
            if (fromId < 1 || quantity < 1)
            {
                throw new ArgumentException(FormattableString.Invariant($"A load of {item} names a building of at least 1 and a quantity of at least 1, not {fromId} and {quantity}."));
            }

            Item = item;
            FromId = fromId;
            Quantity = quantity;
        }

        /// <summary>The good carried.</summary>
        public ItemId Item { get; }

        /// <summary>The building it was taken from: where it goes back to when the courier stops working.</summary>
        public int FromId { get; }

        /// <summary>How many of the good, at least 1.</summary>
        public long Quantity { get; }

        /// <summary>The order of a courier's load: by good (ordinal), then by the building it came from (ascending id).</summary>
        internal static int Compare(CourierLoad left, CourierLoad right)
        {
            int byItem = left.Item.CompareTo(right.Item);
            return byItem != 0 ? byItem : left.FromId.CompareTo(right.FromId);
        }
    }
}
