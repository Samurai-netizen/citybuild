using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>
/// Moving goods, the same for every strategy (docs/modules/BalanceSim.md → Strategies; core-instructions/04-simulation-model.md §16–§19):
/// <list type="number">
/// <item>Routes (<see cref="Routes"/>): once a courier station is built and routes are unlocked, the couriers run one loop per good.</item>
/// <item>Project deliveries: every construction site, waiting upgrade and waiting research that no route serves gets what it still needs
/// from the buildings that hold the good and don't consume it, largest stock first.</item>
/// <item>Input deliveries: a producer no route serves that holds less than two cycles of an input gets one delivery of it.</item>
/// </list>
/// Deliveries go by courier with a built station, else by hand (a free worker). No roads are built.
/// </summary>
public static class Logistics
{
    /// <summary>Re-energizes the couriers (at the start of a check-in), sets the routes, and sends the deliveries.</summary>
    /// <param name="turn">The turn.</param>
    public static void Run(Turn turn)
    {
        if (turn.IsSessionStart && turn.City.Couriers.Count > 0)
        {
            turn.Actions.ReenergizeCouriers();
        }

        HashSet<(int Building, ItemId Item)> served = Routes.Set(turn);
        foreach ((Building target, ItemId item, long need) in ProjectNeeds(turn))
        {
            long missing = need - Pending(turn.City, target.Id, item);
            if (!served.Contains((target.Id, item)) && missing > 0 && !TrySend(turn, target, item, missing))
            {
                return;
            }
        }

        foreach (Building consumer in turn.City.Buildings)
        {
            if (consumer.Phase is not (BuildingPhase.Idle or BuildingPhase.Producing)
                || turn.DefinitionOf(consumer).GetLevel(consumer.Level).Recipe is not { } recipe)
            {
                continue;
            }

            foreach (ItemQuantity input in recipe.Inputs)
            {
                if (!served.Contains((consumer.Id, input.Item)) && consumer.GetStock(input.Item) < input.Quantity * 2
                    && Pending(turn.City, consumer.Id, input.Item) == 0)
                {
                    long room = CityQueries.Room(turn.City, turn.Content, consumer, input.Item);
                    if (room > 0 && !TrySend(turn, consumer, input.Item, room))
                    {
                        return;
                    }
                }
            }
        }
    }

    /// <summary>What every waiting project still needs: construction sites and waiting upgrades (by id), then the running research's goods at the science building.</summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The target, the good and the quantity it still needs, in that order.</returns>
    internal static List<(Building Target, ItemId Item, long Need)> ProjectNeeds(Turn turn)
    {
        var needs = new List<(Building Target, ItemId Item, long Need)>();
        foreach (Building building in turn.City.Buildings)
        {
            needs.AddRange(building.Needs.OrderBy(need => need.Key.Value, StringComparer.Ordinal).Select(need => (building, need.Key, need.Value)));
        }

        if (turn.City.ActiveResearch is { IsWaiting: true } research && CityQueries.ScienceBuilding(turn.City, turn.Content) is { } science)
        {
            needs.AddRange(research.Needs.OrderBy(need => need.Key.Value, StringComparer.Ordinal).Select(need => (science, need.Key, need.Value)));
        }

        return needs;
    }

    /// <summary>
    /// Sends one delivery of up to <paramref name="wanted"/> of a good to a target from the building that holds most of it (and doesn't use
    /// it as a recipe input), when a carrier is free.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="target">The building the goods go to.</param>
    /// <param name="item">The good.</param>
    /// <param name="wanted">How many it should get, above 0.</param>
    /// <returns>False when no carrier is free (the caller stops sending this step), else true (sent or nothing to send).</returns>
    private static bool TrySend(Turn turn, Building target, ItemId item, long wanted)
    {
        if (!HasFreeCarrier(turn))
        {
            return false;
        }

        Building? source = turn.City.Buildings
            .Where(building => building.Id != target.Id && building.GetStock(item) > 0
                && !ContentChain.Inputs(turn.DefinitionOf(building)).Contains(item))
            .OrderByDescending(building => building.GetStock(item)).ThenBy(building => building.Id)
            .FirstOrDefault();
        if (source is null || DeliveryAmount.Choose(turn, source.GetStock(item), wanted) is not { } amount)
        {
            return true;
        }

        long quantity = DeliveryAmount.QuantityOf(turn, amount, source.GetStock(item));
        if (!QueueHasRoom(turn, quantity))
        {
            return false;
        }

        turn.Actions.SendDelivery(source.Id, target.Id, item, amount);
        return true;
    }

    /// <summary>
    /// Whether a delivery can go now: with a built courier station, a courier without a route whose queue has room; without one, a free
    /// worker (04 §18).
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>True when a carrier is free.</returns>
    private static bool HasFreeCarrier(Turn turn)
    {
        CityState city = turn.City;
        if (!CityQueries.HasBuiltCourierStation(city, turn.Content))
        {
            return CityQueries.FreeWorkers(city, turn.Content) > 0;
        }

        return city.Couriers.Any(courier => !city.TryGetRoute(courier.StationId, courier.Slot, out _)
            && city.CountLines(courier.StationId, courier.Slot) < turn.Content.Rules.MaxDeliveryLines);
    }

    /// <summary>
    /// Whether the courier the engine would pick has room for the delivery's lines (one per carry limit, 04 §18): every courier without a
    /// route that has the fewest lines (the engine picks among them by walk) must fit <c>ceil(quantity / carry)</c> more. Always true
    /// without a built station (a hand delivery has no queue).
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="quantity">The goods the delivery counts.</param>
    /// <returns>True when the engine won't answer <c>DeliveryQueueFull</c>.</returns>
    private static bool QueueHasRoom(Turn turn, long quantity)
    {
        CityState city = turn.City;
        if (!CityQueries.HasBuiltCourierStation(city, turn.Content))
        {
            return true;
        }

        (Courier Courier, int Lines)[] free = [.. city.Couriers.Where(courier => !city.TryGetRoute(courier.StationId, courier.Slot, out _))
            .Select(courier => (courier, city.CountLines(courier.StationId, courier.Slot)))];
        int fewest = free.Min(entry => entry.Lines);
        return free.Where(entry => entry.Lines == fewest).All(entry =>
        {
            long carry = city.TryGetBuilding(entry.Courier.StationId, out Building? station)
                ? turn.DefinitionOf(station).GetLevel(station.Level).CarryLimit
                : 1;
            return entry.Lines + ((quantity + carry - 1) / Math.Max(1, carry)) <= turn.Content.Rules.MaxDeliveryLines;
        });
    }

    /// <summary>The goods already on their way to a building: trips (this load and the rest) and courier delivery lines.</summary>
    /// <param name="city">The city.</param>
    /// <param name="targetId">The building.</param>
    /// <param name="item">The good.</param>
    /// <returns>The quantity.</returns>
    private static long Pending(CityState city, int targetId, ItemId item) =>
        city.Trips.Where(trip => trip.TargetId == targetId && trip.Item == item).Sum(trip => trip.Quantity + trip.Remaining)
        + city.DeliveryLines.Where(line => line.TargetId == targetId && line.Item == item).Sum(line => line.Quantity);
}
