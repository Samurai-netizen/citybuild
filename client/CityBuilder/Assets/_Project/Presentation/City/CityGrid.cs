#nullable enable

using System;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The shape of the city grid in the world and the conversions between world positions and tiles. The size comes from the content's
    /// rules (<c>GameRules.GridWidth</c> and <c>GridHeight</c>). Tiles are one world unit wide. The ground is the plane y = 0; column
    /// <c>x</c> runs along the world's x axis and row <c>y</c> along its z axis, so tile (0, 0) spans (0, 0) to (1, 1). Display only:
    /// whether a building may stand on a tile is the server's decision.
    /// </summary>
    public sealed class CityGrid
    {
        /// <summary>The width and depth of one tile in world units.</summary>
        public const float TileSize = 1f;

        /// <summary>Creates a grid of <paramref name="width"/> by <paramref name="height"/> tiles.</summary>
        /// <param name="width">The number of columns, at least 1.</param>
        /// <param name="height">The number of rows, at least 1.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is below 1.</exception>
        public CityGrid(int width, int height)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "The grid needs at least one column.");
            }

            if (height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "The grid needs at least one row.");
            }

            Width = width;
            Height = height;
        }

        /// <summary>The number of columns.</summary>
        public int Width { get; }

        /// <summary>The number of rows.</summary>
        public int Height { get; }

        /// <summary>The world size of the whole grid on the ground: x is the width, y the depth (the world's z).</summary>
        public Vector2 WorldSize => new Vector2(Width * TileSize, Height * TileSize);

        /// <summary>The middle of the grid on the ground: x is the world's x, y the world's z.</summary>
        public Vector2 WorldCenter => WorldSize * 0.5f;

        /// <summary>Whether <paramref name="coordinate"/> is a tile of this grid.</summary>
        public bool Contains(GridCoordinate coordinate)
        {
            return coordinate.X >= 0 && coordinate.X < Width && coordinate.Y >= 0 && coordinate.Y < Height;
        }

        /// <summary>The world position of the middle of a tile, on the ground (y = 0). The tile need not be inside the grid.</summary>
        public Vector3 ToWorldCenter(GridCoordinate coordinate)
        {
            return new Vector3((coordinate.X + 0.5f) * TileSize, 0f, (coordinate.Y + 0.5f) * TileSize);
        }

        /// <summary>
        /// The tile under a world position: only x and z count, and a tile owns its west and south edges, not its east and north ones. Returns
        /// false, and the default coordinate, when the position is outside the grid or not a number.
        /// </summary>
        public bool TryGetCoordinate(Vector3 worldPosition, out GridCoordinate coordinate)
        {
            float column = worldPosition.x / TileSize;
            float row = worldPosition.z / TileSize;

            // Written so that a NaN fails the test and is outside.
            if (!(column >= 0f && column < Width && row >= 0f && row < Height))
            {
                coordinate = default;
                return false;
            }

            coordinate = new GridCoordinate((int)Mathf.Floor(column), (int)Mathf.Floor(row));
            return true;
        }

        /// <summary>Moves a ground position (x, and z as y) to the nearest point inside the grid's outline; a position inside stays as it is.</summary>
        public Vector2 ClampToBounds(Vector2 groundPosition)
        {
            Vector2 size = WorldSize;
            return new Vector2(Mathf.Clamp(groundPosition.x, 0f, size.x), Mathf.Clamp(groundPosition.y, 0f, size.y));
        }
    }
}
