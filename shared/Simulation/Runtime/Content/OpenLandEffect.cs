#nullable enable
namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Opens an area of the map from its research's completion on (<c>29</c> → Map, <c>04</c> section 17): buildings and roads may stand on
    /// its tiles and walkers may cross them. Derived from the completed research, never stored. JSON: <c>openLand</c> with <c>area</c>.
    /// </summary>
    public sealed class OpenLandEffect : ResearchEffect
    {
        /// <summary>Creates the effect as read from an <c>openLand</c> entry.</summary>
        internal OpenLandEffect(char area)
        {
            Area = area;
        }

        /// <summary>The area letter it opens: one of the map's, not its start area.</summary>
        public char Area { get; }
    }
}
