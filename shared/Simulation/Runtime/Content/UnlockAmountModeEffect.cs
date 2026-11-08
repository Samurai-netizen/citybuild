#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>Allows an amount mode in trips and routes from its research's completion on (<c>04</c> section 19). JSON: <c>unlockAmountMode</c>.</summary>
    public sealed class UnlockAmountModeEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from an <c>unlockAmountMode</c> entry.</summary>
        internal UnlockAmountModeEffect(AmountMode mode)
        {
            Mode = mode;
        }

        /// <summary>The mode it unlocks.</summary>
        public AmountMode Mode { get; }
    }
}
