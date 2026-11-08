#nullable enable
using System;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>Makes new cities from the content.</summary>
    public static class CityFactory
    {
        /// <summary>
        /// Creates a new player's city as the content's starting city describes it: its era and the coins in its purse, clock 0, no buildings
        /// (so no goods: they live in buildings, CR-0008 D2), no research, and the first building id 1.
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

            return new CityState(
                clock: 0,
                era: content.StartingCity.Era,
                nextBuildingId: 1,
                purse: content.StartingCity.Purse,
                buildings: Array.Empty<Building>(),
                completedResearch: Array.Empty<ResearchId>(),
                activeResearch: null);
        }
    }
}
