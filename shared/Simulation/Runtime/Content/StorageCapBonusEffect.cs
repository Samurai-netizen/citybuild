#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>Raises the storage cap of a capped item. Bonuses add to the base cap in <see cref="GameRules.BaseStorageCaps"/>. JSON: <c>storageCapBonus</c>.</summary>
    public sealed class StorageCapBonusEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from a <c>storageCapBonus</c> entry; the rules check its item and amount afterwards.</summary>
        internal StorageCapBonusEffect(ItemId item, long amount)
        {
            Item = item;
            Amount = amount;
        }

        /// <summary>The capped item whose cap rises.</summary>
        public ItemId Item { get; }

        /// <summary>How much the cap rises, in items (greater than zero).</summary>
        public long Amount { get; }
    }
}
