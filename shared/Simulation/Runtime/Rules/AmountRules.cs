#nullable enable
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// The amount rules of <c>core-instructions/04-simulation-model.md</c>, section 19, shared by trips and (part G) couriers' routes: whether a
    /// city may use an amount, and what it gives of a base.
    /// </summary>
    internal static class AmountRules
    {
        /// <summary>
        /// Checks an amount against the content and the city: a preset the content doesn't have is
        /// <see cref="MutationErrorCode.InvalidArgument"/>, a percentage or exact number whose mode no completed research unlocked is
        /// <see cref="MutationErrorCode.AmountModeLocked"/>. Returns null when the city may use it.
        /// </summary>
        public static MutationErrorCode? Check(CityState city, GameContent content, Amount amount)
        {
            if (!amount.Mode.HasValue)
            {
                return content.TryGetAmountPreset(amount.PresetId, out _) ? (MutationErrorCode?)null : MutationErrorCode.InvalidArgument;
            }

            return CityQueries.IsAmountModeUnlocked(city, content, amount.Mode.Value) ? (MutationErrorCode?)null : MutationErrorCode.AmountModeLocked;
        }

        /// <summary>
        /// What an amount gives of a base <paramref name="baseQuantity"/> (a store's stock, a carried quantity), before the caller's limits
        /// (<c>04</c> section 19): a preset or percentage <c>p</c> gives <c>max(1, b × p / 100</c> rounded down<c>)</c> when <c>b ≥ 1</c>, else 0;
        /// an exact number gives itself. The amount passed <see cref="Check"/>.
        /// </summary>
        public static long Of(GameContent content, Amount amount, long baseQuantity)
        {
            if (amount.Mode == AmountMode.Exact)
            {
                return amount.Value;
            }

            if (baseQuantity < 1)
            {
                return 0;
            }

            long percent = amount.Mode == AmountMode.Percent ? amount.Value : PresetPercent(content, amount);
            long share = IntegerMath.FloorDiv(checked(baseQuantity * percent), 100);
            return share < 1 ? 1 : share;
        }

        /// <summary>The percentage of a preset the content has (checked by <see cref="Check"/>).</summary>
        private static long PresetPercent(GameContent content, Amount amount)
        {
            content.TryGetAmountPreset(amount.PresetId, out AmountPresetDefinition? preset);
            return preset!.Percent;
        }
    }
}
