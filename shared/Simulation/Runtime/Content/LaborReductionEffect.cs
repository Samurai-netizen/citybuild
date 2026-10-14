#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Lowers the labor of a producer's recipe at every level. The reductions of all completed research add up, and the effective labor is
    /// <c>IntegerMath.ApplyPermilleReduction(labor, sum)</c>. JSON: <c>laborReduction</c>.
    /// </summary>
    public sealed class LaborReductionEffect : ResearchEffect
    {
        internal LaborReductionEffect(BuildingTypeId building, int permille)
        {
            Building = building;
            Permille = permille;
        }

        /// <summary>The producer type whose labor is lowered.</summary>
        public BuildingTypeId Building { get; }

        /// <summary>The reduction in permille (200 = 20 %), from 1 to 1000. The sum per building type is at most <see cref="GameRules.MaxLaborReductionPermille"/>.</summary>
        public int Permille { get; }
    }
}
