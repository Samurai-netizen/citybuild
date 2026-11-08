#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// One level of the road a player can build on an empty tile (<c>core-instructions/04-simulation-model.md</c>, section 17): how fast a
    /// walker crosses it and what a tile costs. Level 1 is placed; every higher level is an upgrade of a tile already at the level below.
    /// </summary>
    public sealed class RoadLevelDefinition
    {
        /// <summary>
        /// Creates the level from values the shape phase has read (<see cref="ContentReader"/>); the content rules check them afterwards. Each
        /// value is described on its property.
        /// </summary>
        internal RoadLevelDefinition(int level, string name, long tileMillis, IReadOnlyList<ItemQuantity> cost)
        {
            Level = level;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TileMillis = tileMillis;
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
        }

        /// <summary>The level number, from 1 (placed) to the last road level.</summary>
        public int Level { get; }

        /// <summary>The display name, for example <c>Dirt path</c>.</summary>
        public string Name { get; }

        /// <summary>How long a walker takes to cross one tile of this road, in milliseconds: below the ground time and below the level before.</summary>
        public long TileMillis { get; }

        /// <summary>What one tile costs, sorted by item id: to place it (level 1) or to upgrade it to this level (above).</summary>
        public IReadOnlyList<ItemQuantity> Cost { get; }
    }
}
