#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// An amount of one item in content: one line of a cost, a recipe, a storage cap or the starting inventory. Lists of them are sorted
    /// by item id (ordinal) and name each item at most once.
    /// </summary>
    public sealed class ItemQuantity
    {
        internal ItemQuantity(ItemId item, long quantity)
        {
            Item = item;
            Quantity = quantity;
        }

        /// <summary>The item.</summary>
        public ItemId Item { get; }

        /// <summary>How many of the item. Greater than zero in recipes, bonuses and caps; at least zero in costs and the starting inventory.</summary>
        public long Quantity { get; }
    }
}
