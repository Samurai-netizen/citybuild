#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Makes every walk of the city shorter from its research's completion on (<c>04</c> section 17): the walk bonuses of completed research
    /// add up and reduce a walk's double milliseconds with <c>IntegerMath.ApplyPermilleReduction</c>. JSON: <c>walkSpeedBonus</c>.
    /// </summary>
    public sealed class WalkSpeedBonusEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from a <c>walkSpeedBonus</c> entry; the rules check its permille afterwards.</summary>
        internal WalkSpeedBonusEffect(int permille)
        {
            Permille = permille;
        }

        /// <summary>How much shorter every walk gets, 1 to 1000 permille.</summary>
        public int Permille { get; }
    }
}
