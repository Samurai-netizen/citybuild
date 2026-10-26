#nullable enable
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The reserved quantities during one advance: what <see cref="CityQueries.Reserved"/> would give, kept up to date instead of summed over
    /// every building for each capped output a start checks (the worst case of <c>04</c> section 12 did that about 200 times per event time).
    /// Inside an advance only two things change a reservation: a cycle that starts reserves its outputs, and a cycle that completes delivers
    /// them. Mutations run between advances, and the table is made new for each one, so it never outlives the city it was taken from.
    /// </summary>
    internal sealed class Reservations
    {
        /// <summary>The reserved quantity per item, sorted by item id; reading and changing it allocates nothing.</summary>
        private readonly ItemTotals _reserved = new ItemTotals();

        /// <summary>Creates an empty table; <see cref="Of"/> fills it from a city.</summary>
        private Reservations()
        {
        }

        /// <summary>The outputs of every producing building of the city, frozen cycles included, as <see cref="CityQueries.Reserved"/> sums them.</summary>
        public static Reservations Of(CityState city, GameContent content)
        {
            var reservations = new Reservations();
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                Building building = buildings[index];
                if (building.Phase == BuildingPhase.Producing)
                {
                    reservations.Add(CityQueries.RecipeOf(content, building.TypeId, building.Level).Outputs);
                }
            }

            return reservations;
        }

        /// <summary>The reserved quantity of an item, 0 when no running cycle makes it.</summary>
        public long Get(ItemId item) => _reserved.Get(item);

        /// <summary>A cycle started: its outputs are reserved.</summary>
        public void Add(IReadOnlyList<ItemQuantity> outputs)
        {
            for (int index = 0; index < outputs.Count; index++)
            {
                ItemQuantity output = outputs[index];
                _reserved.Set(output.Item, checked(_reserved.Get(output.Item) + output.Quantity));
            }
        }

        /// <summary>A cycle completed: its outputs are in stock now, no longer reserved.</summary>
        public void Remove(IReadOnlyList<ItemQuantity> outputs)
        {
            for (int index = 0; index < outputs.Count; index++)
            {
                ItemQuantity output = outputs[index];
                _reserved.Set(output.Item, checked(_reserved.Get(output.Item) - output.Quantity));
            }
        }
    }
}
