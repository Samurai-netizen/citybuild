#nullable enable
using System;

namespace CityBuilder.Simulation.Map
{
    /// <summary>
    /// The one formula that turns a walk's tiles into whole seconds (<c>core-instructions/04-simulation-model.md</c>, section 17). A step costs
    /// half of the tile it leaves plus half of the tile it enters; to stay in integers the engine sums <c>leave + enter</c> over the steps (the
    /// "double milliseconds" <c>D</c>, twice the walk's real milliseconds), applies the walk bonus, and rounds up once.
    /// </summary>
    public static class WalkTime
    {
        /// <summary>Double milliseconds in one second: <c>D</c> counts every millisecond twice.</summary>
        public const long DoubleMillisPerSecond = 2000;

        /// <summary>
        /// The seconds a walk takes: <c>max(1, CeilDiv(ApplyPermilleReduction(D, bonus), 2000))</c>. Every walk takes at least one second, also a
        /// walk of no step (from a building to itself), so a walker's arrival is always after its departure.
        /// </summary>
        /// <param name="doubleMillis">The walk's <c>D</c>: the sum of <c>leave + enter</c> over its steps, at least 0.</param>
        /// <param name="bonusPermille">The walk bonus of the city's completed research, 0 to 1000 permille.</param>
        /// <returns>The walk's seconds, at least 1.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="doubleMillis"/> is negative or <paramref name="bonusPermille"/> is outside 0 to 1000.</exception>
        public static long Seconds(long doubleMillis, int bonusPermille) =>
            Math.Max(1, IntegerMath.CeilDiv(IntegerMath.ApplyPermilleReduction(doubleMillis, bonusPermille), DoubleMillisPerSecond));

        /// <summary>
        /// The seconds of the shortest walk there can be: one step between two adjacent buildings, both counting as ground (content rule 12
        /// holds it to <see cref="Content.GameRules.MinWalkSeconds"/>).
        /// </summary>
        /// <param name="groundTileMillis">The ground time per tile in milliseconds, greater than 0.</param>
        /// <param name="bonusPermille">The walk bonus, 0 to 1000 permille.</param>
        /// <returns>The seconds of that walk, at least 1.</returns>
        public static long ShortestWalkSeconds(long groundTileMillis, int bonusPermille) =>
            Seconds(checked(groundTileMillis + groundTileMillis), bonusPermille);
    }
}
