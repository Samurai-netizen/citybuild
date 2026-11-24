#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Marks the city chartered from its research's completion on: the end of Era I (<c>25</c> → The end of Era I: the Town Charter). Derived
    /// from the completed research, never stored; until Era II exists nothing else changes. JSON: <c>grantCharter</c>, with no values.
    /// </summary>
    public sealed class GrantCharterEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from a <c>grantCharter</c> entry.</summary>
        internal GrantCharterEffect()
        {
        }
    }
}
