#nullable enable
namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Lets the city place bridges on water from its research's completion on (<see cref="BridgeDefinition"/>, M1-03). Derived from the
    /// completed research, never stored. JSON: <c>unlockBridge</c>, with no values.
    /// </summary>
    public sealed class UnlockBridgeEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from an <c>unlockBridge</c> entry.</summary>
        internal UnlockBridgeEffect()
        {
        }
    }
}
