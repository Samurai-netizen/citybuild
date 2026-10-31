#nullable enable

using System;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// A tile of the city grid: the same <c>x</c> and <c>y</c> a building has in the engine (<c>Building.X</c>, <c>Building.Y</c>). A plain pair
    /// of integers; whether it lies inside the grid depends on the grid's size, which <see cref="CityGrid"/> knows.
    /// </summary>
    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        /// <summary>Creates a coordinate.</summary>
        /// <param name="x">The column, 0 at the west edge.</param>
        /// <param name="y">The row, 0 at the south edge.</param>
        public GridCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>The column; it runs along the world's x axis.</summary>
        public int X { get; }

        /// <summary>The row; it runs along the world's z axis.</summary>
        public int Y { get; }

        /// <summary>Whether <paramref name="other"/> is the same tile.</summary>
        public bool Equals(GridCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <summary>Whether <paramref name="obj"/> is a <see cref="GridCoordinate"/> for the same tile.</summary>
        public override bool Equals(object? obj)
        {
            return obj is GridCoordinate other && Equals(other);
        }

        /// <summary>A hash code that agrees with <see cref="Equals(GridCoordinate)"/>.</summary>
        public override int GetHashCode()
        {
            return unchecked((X * 397) ^ Y);
        }

        /// <summary>The tile as <c>(x, y)</c>, for logs and test messages.</summary>
        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }

        /// <summary>Whether the two coordinates are the same tile.</summary>
        public static bool operator ==(GridCoordinate left, GridCoordinate right)
        {
            return left.Equals(right);
        }

        /// <summary>Whether the two coordinates are different tiles.</summary>
        public static bool operator !=(GridCoordinate left, GridCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}
