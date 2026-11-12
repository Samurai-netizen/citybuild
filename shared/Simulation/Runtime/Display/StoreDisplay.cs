#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Display
{
    /// <summary>
    /// How full a building's store is, for the store gauge over a building (CR-0008 D25). <b>Display only, never authority.</b> Whole permille,
    /// rounded down with <see cref="IntegerMath.FloorDiv"/>, no floating point.
    /// </summary>
    public static class StoreDisplay
    {
        /// <summary>
        /// The fullest good of a building's store: the stock of each good its level holds against that good's effective limit (<c>04</c>
        /// section 16), in permille, at most 1000 (a refund or a returned load may go above the limit). On a tie the first good in the level's
        /// storage order wins. A store with no goods, or a building whose type or level the content doesn't know, gives 0 and the empty id.
        /// </summary>
        /// <param name="city">The city, whose completed research raises the limits.</param>
        /// <param name="content">The game content.</param>
        /// <param name="building">The building.</param>
        /// <param name="item">The fullest good, or the empty id.</param>
        /// <returns>0 to 1000.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static int FullestPermille(CityState city, GameContent content, Building building, out ItemId item)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (building is null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            item = default;
            if (!content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition) || building.Level > definition.MaxLevel)
            {
                return 0;
            }

            int fullest = 0;
            IReadOnlyList<ItemQuantity> storage = definition.GetLevel(building.Level).Storage;
            for (int index = 0; index < storage.Count; index++)
            {
                ItemId good = storage[index].Item;
                long stock = building.GetStock(good);
                if (stock < 1 || !CityQueries.TryGetEffectiveLimit(city, content, building, good, out long limit) || limit < 1)
                {
                    continue;
                }

                int permille = (int)Math.Min(1000, IntegerMath.FloorDiv(checked(stock * 1000), limit));
                if (permille > fullest)
                {
                    fullest = permille;
                    item = good;
                }
            }

            return fullest;
        }
    }
}
