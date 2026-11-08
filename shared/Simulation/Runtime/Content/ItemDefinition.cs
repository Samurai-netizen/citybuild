#nullable enable
using System;

namespace CityBuilder.Simulation.Content
{
    /// <summary>An item of the game content, such as coins, logs or planks.</summary>
    public sealed class ItemDefinition
    {
        /// <summary>Creates the item from values the shape phase has read (<see cref="ContentReader"/>); each is described on its property.</summary>
        internal ItemDefinition(ItemId id, string name, int era, bool isCurrency, bool isTradable)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Era = era;
            IsCurrency = isCurrency;
            IsTradable = isTradable;
        }

        /// <summary>The item's id.</summary>
        public ItemId Id { get; }

        /// <summary>The display name, for example <c>Planks</c>.</summary>
        public string Name { get; }

        /// <summary>The number of the era the item belongs to.</summary>
        public int Era { get; }

        /// <summary>
        /// Whether the item is the currency (exactly one item is): it lives in the city's purse, uncapped and in no building, is paid and
        /// received at once, and is never carried (<c>04</c> section 16). Every other item is a good, kept in building stores.
        /// </summary>
        public bool IsCurrency { get; }

        /// <summary>Whether the item may be traded on the player market (from Era II, <c>core-instructions/26-open-market.md</c>).</summary>
        public bool IsTradable { get; }
    }
}
