#nullable enable
using System;

namespace CityBuilder.Simulation.Map
{
    /// <summary>
    /// What the best walk between two tiles costs (<c>core-instructions/04-simulation-model.md</c>, section 17): the buildings it crosses and
    /// its double milliseconds <c>D</c> (the sum of <c>leave + enter</c> over its steps). The path rule orders walks by crossings, then by
    /// <c>D</c>; the walk bonus scales every walk alike, so it never changes which walk is best and is applied only in <see cref="Seconds"/>.
    /// </summary>
    public readonly struct WalkCost : IEquatable<WalkCost>
    {
        /// <summary>Creates a cost.</summary>
        /// <param name="crossings">The other buildings' tiles the walk enters, at least 0.</param>
        /// <param name="doubleMillis">The walk's <c>D</c>, at least 0.</param>
        public WalkCost(int crossings, long doubleMillis)
        {
            Crossings = crossings;
            DoubleMillis = doubleMillis;
        }

        /// <summary>How many other buildings' tiles the walk enters (the walk's own start and end don't count).</summary>
        public int Crossings { get; }

        /// <summary>The walk's double milliseconds: <c>Σ (leave + enter)</c> over its steps, twice its real milliseconds.</summary>
        public long DoubleMillis { get; }

        /// <summary>The walk's seconds with a walk bonus (<see cref="WalkTime.Seconds"/>): at least 1.</summary>
        /// <param name="bonusPermille">The city's walk bonus, 0 to 1000 permille.</param>
        /// <returns>The seconds the walk takes.</returns>
        public long Seconds(int bonusPermille) => WalkTime.Seconds(DoubleMillis, bonusPermille);

        /// <summary>Whether two costs are equal.</summary>
        /// <param name="other">The other cost.</param>
        /// <returns>True when crossings and <c>D</c> are equal.</returns>
        public bool Equals(WalkCost other) => Crossings == other.Crossings && DoubleMillis == other.DoubleMillis;

        /// <summary>Whether <paramref name="obj"/> is an equal cost.</summary>
        /// <param name="obj">Any object.</param>
        /// <returns>True for an equal <see cref="WalkCost"/>.</returns>
        public override bool Equals(object? obj) => obj is WalkCost other && Equals(other);

        /// <summary>A hash consistent with <see cref="Equals(WalkCost)"/>.</summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode() => unchecked((Crossings * 397) ^ DoubleMillis.GetHashCode());

        /// <summary>The cost as text, for test messages.</summary>
        /// <returns>For example <c>0 crossings, 20000 double ms</c>.</returns>
        public override string ToString() => FormattableString.Invariant($"{Crossings} crossings, {DoubleMillis} double ms");

        /// <summary>Whether two costs are equal.</summary>
        /// <param name="left">One cost.</param>
        /// <param name="right">The other cost.</param>
        /// <returns>True when equal.</returns>
        public static bool operator ==(WalkCost left, WalkCost right) => left.Equals(right);

        /// <summary>Whether two costs differ.</summary>
        /// <param name="left">One cost.</param>
        /// <param name="right">The other cost.</param>
        /// <returns>True when not equal.</returns>
        public static bool operator !=(WalkCost left, WalkCost right) => !left.Equals(right);
    }
}
