#nullable enable
using System;

namespace CityBuilder.Simulation.Content
{
    /// <summary>An item of the game content, such as coins, logs or planks.</summary>
    public sealed class ItemDefinition
    {
        /// <summary>Creates the item from values the shape phase has read (<see cref="ContentReader"/>); each is described on its property.</summary>
        internal ItemDefinition(ItemId id, string name, int era, bool isCapped, bool isTradable)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Era = era;
            IsCapped = isCapped;
            IsTradable = isTradable;
        }

        /// <summary>The item's id.</summary>
        public ItemId Id { get; }

        /// <summary>The display name, for example <c>Planks</c>.</summary>
        public string Name { get; }

        /// <summary>The number of the era the item belongs to.</summary>
        public int Era { get; }

        /// <summary>
        /// Whether production of the item stops at its storage cap (the base cap in <see cref="GameRules.BaseStorageCaps"/> plus research
        /// bonuses). Coins are uncapped.
        /// </summary>
        public bool IsCapped { get; }

        /// <summary>Whether the item may be traded on the player market (from Era II, <c>core-instructions/26-open-market.md</c>).</summary>
        public bool IsTradable { get; }
    }
}
