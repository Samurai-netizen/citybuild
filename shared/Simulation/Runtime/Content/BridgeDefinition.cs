#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The bridge (<c>04</c> section 17, M1-03): the road kind that stands only on water, once a research with <see cref="UnlockBridgeEffect"/>
    /// is done. It has one level: placed, never upgraded; removing it is free, like a road's.
    /// </summary>
    public sealed class BridgeDefinition
    {
        /// <summary>Creates the bridge from values the shape phase has read; the content rules check them afterwards.</summary>
        internal BridgeDefinition(string name, long tileMillis, IReadOnlyList<ItemQuantity> cost)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TileMillis = tileMillis;
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
        }

        /// <summary>The display name, for example <c>Bridge</c>.</summary>
        public string Name { get; }

        /// <summary>How long a walker takes to cross one bridge tile, in milliseconds: below the ground time.</summary>
        public long TileMillis { get; }

        /// <summary>What one bridge tile costs to place, sorted by item id: the currency only.</summary>
        public IReadOnlyList<ItemQuantity> Cost { get; }
    }
}
