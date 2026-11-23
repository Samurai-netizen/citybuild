#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Raises one building type's max level from its research's completion on (<c>04</c> section 8; <c>25</c> → Effect types): a type's max
    /// level is its <see cref="BuildingDefinition.BaseMaxLevel"/> plus the bonuses of completed research, never above its last level
    /// (content rule). JSON: <c>maxLevelBonus</c>.
    /// </summary>
    public sealed class MaxLevelBonusEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from a <c>maxLevelBonus</c> entry; the rules check its target and amount afterwards.</summary>
        internal MaxLevelBonusEffect(BuildingTypeId building, long amount)
        {
            Building = building;
            Amount = amount;
        }

        /// <summary>The building type whose max level rises; it has a level above its base max level.</summary>
        public BuildingTypeId Building { get; }

        /// <summary>How many levels the max level rises (greater than zero).</summary>
        public long Amount { get; }
    }
}
