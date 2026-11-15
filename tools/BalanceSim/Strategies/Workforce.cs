using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>
/// Worker assignment, the same for every strategy (docs/modules/BalanceSim.md → Strategies): courier stations are filled first (each worker is
/// a courier), then the producers get one worker at a time in chain order (raw first, then by id) until the workers or the slots run out.
/// An upgrading building gets none (its workers would do nothing). Until a courier station is built, one worker stays free for hand deliveries.
/// </summary>
public static class Workforce
{
    /// <summary>The workers kept free for hand deliveries: 1 until a courier station is built (then couriers carry), else 0.</summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The reserve.</returns>
    public static long HandDeliveryReserve(Turn turn) => CityQueries.HasBuiltCourierStation(turn.City, turn.Content) ? 0 : 1;

    /// <summary>Works out the assignment and applies it: decreases first, so every increase fits the free workers.</summary>
    /// <param name="turn">The turn.</param>
    public static void Assign(Turn turn)
    {
        CityState city = turn.City;
        long pool = CityQueries.WorkersProvided(city, turn.Content) - city.Trips.Count - HandDeliveryReserve(turn);
        var desired = new SortedDictionary<int, int>();
        var producers = new List<(Building Building, int Max, int Order)>();
        foreach (Building building in city.Buildings)
        {
            BuildingDefinition definition = turn.DefinitionOf(building);
            if (definition.Kind is not (BuildingKind.Producer or BuildingKind.CourierStation) || building.Phase == BuildingPhase.Constructing)
            {
                continue;
            }

            desired[building.Id] = 0;
            int max = building.Phase == BuildingPhase.Upgrading ? 0 : definition.GetLevel(building.Level).MaxWorkers;
            if (definition.Kind == BuildingKind.CourierStation)
            {
                int give = (int)Math.Clamp(pool, 0, max);
                desired[building.Id] = give;
                pool -= give;
            }
            else
            {
                producers.Add((building, max, turn.Chain.IndexOf(building.TypeId)));
            }
        }

        (Building Building, int Max, int Order)[] ordered = [.. producers.OrderBy(entry => entry.Order).ThenBy(entry => entry.Building.Id)];
        bool gave = true;
        while (pool > 0 && gave)
        {
            gave = false;
            foreach ((Building building, int max, _) in ordered)
            {
                if (pool > 0 && desired[building.Id] < max)
                {
                    desired[building.Id]++;
                    pool--;
                    gave = true;
                }
            }
        }

        foreach (bool decreases in new[] { true, false })
        {
            foreach ((int id, int workers) in desired)
            {
                if (city.TryGetBuilding(id, out Building? building) && building.AssignedWorkers != workers
                    && (workers < building.AssignedWorkers) == decreases)
                {
                    turn.Actions.AssignWorkers(id, workers);
                }
            }
        }
    }

}
