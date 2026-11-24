#nullable enable
using System;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// A road on one tile of a city (<c>core-instructions/04-simulation-model.md</c>, section 17): its tile and level. A road never shares a
    /// tile with a building. Roads are values: an upgrade replaces the tile's road with one a level higher.
    /// </summary>
    public readonly struct RoadTile : IEquatable<RoadTile>
    {
        /// <summary>Creates a road on a tile.</summary>
        /// <param name="x">The tile column, at least 0.</param>
        /// <param name="y">The tile row, at least 0.</param>
        /// <param name="level">The road level, at least 1 (whether the content has that level is the caller's business).</param>
        /// <exception cref="ArgumentOutOfRangeException">A coordinate is negative or the level is below 1.</exception>
        public RoadTile(int x, int y, int level)
            : this(x, y, level, false)
        {
        }

        /// <summary>
        /// Creates a road or a bridge tile (M1-03). A bridge stands on water and has level 1 (<see cref="Content.BridgeDefinition"/>).
        /// </summary>
        /// <param name="x">The tile column, from 0.</param>
        /// <param name="y">The tile row, from 0.</param>
        /// <param name="level">The road level, from 1; 1 for a bridge.</param>
        /// <param name="isBridge">Whether the tile is a bridge.</param>
        /// <exception cref="ArgumentOutOfRangeException">A negative tile, a level below 1, or a bridge above level 1.</exception>
        public RoadTile(int x, int y, int level, bool isBridge)
        {
            if (x < 0 || y < 0 || level < 1 || (isBridge && level != 1))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(level), FormattableString.Invariant($"A road at ({x}, {y}) of level {level}: the tile must not be negative and the level is at least 1."));
            }

            X = x;
            Y = y;
            Level = level;
            IsBridge = isBridge;
        }

        /// <summary>The tile column, from 0 to the grid width − 1.</summary>
        public int X { get; }

        /// <summary>The tile row, from 0 to the grid height − 1.</summary>
        public int Y { get; }

        /// <summary>The road level, from 1 (placed) to the content's last road level.</summary>
        public int Level { get; }

        /// <summary>Whether the tile is a bridge, on water (M1-03); a bridge is crossed at the bridge's time, not a road level's.</summary>
        public bool IsBridge { get; }

        /// <summary>Whether two roads lie on the same tile at the same level, both bridges or both not.</summary>
        /// <param name="other">The other road.</param>
        /// <returns>True when tile and level are equal.</returns>
        public bool Equals(RoadTile other) => X == other.X && Y == other.Y && Level == other.Level && IsBridge == other.IsBridge;

        /// <summary>Whether <paramref name="obj"/> is an equal road.</summary>
        /// <param name="obj">Any object.</param>
        /// <returns>True for a <see cref="RoadTile"/> on the same tile at the same level.</returns>
        public override bool Equals(object? obj) => obj is RoadTile other && Equals(other);

        /// <summary>A hash of the tile and level, consistent with <see cref="Equals(RoadTile)"/>.</summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode() => unchecked(((((X * 397) ^ Y) * 397) ^ Level) * 2 + (IsBridge ? 1 : 0));

        /// <summary>The road as text, for test messages: <c>road (2, 3) level 1</c>.</summary>
        /// <returns>The text.</returns>
        public override string ToString() =>
            IsBridge ? FormattableString.Invariant($"bridge ({X}, {Y})") : FormattableString.Invariant($"road ({X}, {Y}) level {Level}");

        /// <summary>Whether two roads are equal (<see cref="Equals(RoadTile)"/>).</summary>
        /// <param name="left">One road.</param>
        /// <param name="right">The other road.</param>
        /// <returns>True when equal.</returns>
        public static bool operator ==(RoadTile left, RoadTile right) => left.Equals(right);

        /// <summary>Whether two roads differ (<see cref="Equals(RoadTile)"/>).</summary>
        /// <param name="left">One road.</param>
        /// <param name="right">The other road.</param>
        /// <returns>True when not equal.</returns>
        public static bool operator !=(RoadTile left, RoadTile right) => !left.Equals(right);

        /// <summary>The order of roads in a city: by column, then by row (<c>05</c>, the snapshot's <c>roads</c>).</summary>
        /// <param name="left">One road.</param>
        /// <param name="right">The other road.</param>
        /// <returns>Below 0, 0 or above 0 as <paramref name="left"/>'s tile comes before, is, or comes after <paramref name="right"/>'s.</returns>
        internal static int CompareTiles(RoadTile left, RoadTile right) =>
            left.X != right.X ? left.X.CompareTo(right.X) : left.Y.CompareTo(right.Y);
    }
}
