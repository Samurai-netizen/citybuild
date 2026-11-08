#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One tile coordinate <c>{ x, y }</c> in a <see cref="RoadTilesRequestDto"/> (<c>core-instructions/05-api-contract.md</c>, CR-0008).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class RoadTileRequestDto : IEquatable<RoadTileRequestDto>
    {
        /// <summary>Creates the tile coordinate.</summary>
        /// <param name="x">The tile column.</param>
        /// <param name="y">The tile row.</param>
        [JsonConstructor]
        public RoadTileRequestDto(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>The tile column.</summary>
        public int X { get; }

        /// <summary>The tile row.</summary>
        public int Y { get; }

        /// <summary>Value equality by column and row.</summary>
        public bool Equals(RoadTileRequestDto? other) => other != null && X == other.X && Y == other.Y;

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as RoadTileRequestDto);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }
}
