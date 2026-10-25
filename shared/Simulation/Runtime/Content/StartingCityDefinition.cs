#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>What a new city starts with: its era and its stock. It has no buildings, no research and a city clock of 0.</summary>
    public sealed class StartingCityDefinition
    {
        /// <summary>Creates the starting city from values the shape phase has read (<see cref="ContentReader"/>); each is described on its property.</summary>
        internal StartingCityDefinition(int era, IReadOnlyList<ItemQuantity> inventory)
        {
            Era = era;
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        /// <summary>The number of the era a new city starts in.</summary>
        public int Era { get; }

        /// <summary>The starting stock, sorted by item id. Items not listed start at 0.</summary>
        public IReadOnlyList<ItemQuantity> Inventory { get; }
    }
}
