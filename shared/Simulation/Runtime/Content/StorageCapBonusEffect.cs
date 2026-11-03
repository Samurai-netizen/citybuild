#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Raises the limit of a good in every store that holds it (<c>04</c> section 16): bonuses add to each level's limit in
    /// <see cref="BuildingLevelDefinition.Storage"/>. JSON: <c>storageCapBonus</c>.
    /// </summary>
    public sealed class StorageCapBonusEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from a <c>storageCapBonus</c> entry; the rules check its item and amount afterwards.</summary>
        internal StorageCapBonusEffect(ItemId item, long amount)
        {
            Item = item;
            Amount = amount;
        }

        /// <summary>The good whose limit rises; never the currency.</summary>
        public ItemId Item { get; }

        /// <summary>How much the limit rises in every store that holds the good, in items (greater than zero).</summary>
        public long Amount { get; }
    }
}
