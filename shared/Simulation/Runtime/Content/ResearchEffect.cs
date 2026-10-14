#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// What completing a research node changes, as data (<c>core-instructions/25-eras-and-research.md</c>, effect types). The set is
    /// closed: <see cref="UnlockBuildingEffect"/>, <see cref="LaborReductionEffect"/> and <see cref="StorageCapBonusEffect"/>, and only this
    /// assembly can add a type. Effects apply to cycles that start after the research completes; engine code switches on the effect type,
    /// never on a research id.
    /// </summary>
    public abstract class ResearchEffect
    {
        internal ResearchEffect()
        {
        }
    }
}
