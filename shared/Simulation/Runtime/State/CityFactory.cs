#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>Makes new cities from the content.</summary>
    public static class CityFactory
    {
        /// <summary>
        /// Creates a new player's city as the content's starting city describes it: its era and stock, clock 0, no buildings, no research, and
        /// the first building id 1.
        /// </summary>
        /// <param name="content">The game content.</param>
        /// <returns>The new city.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
        public static CityState CreateStarterCity(GameContent content)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var inventory = new List<KeyValuePair<ItemId, long>>();
            foreach (ItemQuantity stock in content.StartingCity.Inventory)
            {
                inventory.Add(new KeyValuePair<ItemId, long>(stock.Item, stock.Quantity));
            }

            return new CityState(
                clock: 0,
                era: content.StartingCity.Era,
                nextBuildingId: 1,
                inventory: inventory,
                buildings: Array.Empty<Building>(),
                completedResearch: Array.Empty<ResearchId>(),
                activeResearch: null);
        }
    }
}
