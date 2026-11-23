#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Adds builder slots from its research's completion on (<c>04</c> section 8; <c>25</c> → Effect types): the slots are
    /// <see cref="GameRules.BuilderSlots"/> plus the bonuses of completed research. JSON: <c>builderSlotBonus</c>.
    /// </summary>
    public sealed class BuilderSlotBonusEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from a <c>builderSlotBonus</c> entry; the rules check its amount afterwards.</summary>
        internal BuilderSlotBonusEffect(long amount)
        {
            Amount = amount;
        }

        /// <summary>The extra builder slots (greater than zero).</summary>
        public long Amount { get; }
    }
}
