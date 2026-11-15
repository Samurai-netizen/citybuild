using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>
/// The couriers' routes, the same for every strategy (docs/modules/BalanceSim.md → Strategies; core-instructions/04-simulation-model.md
/// §19). One loop per good the chain makes, raw goods first: its producers take it, then the projects waiting for it and its consumers put
/// it (a put fills a waiting project first, §16), every action "all". A good with more stops than one route holds is split into balanced
/// slices: slice <c>i</c> of <c>n</c> gets every <c>n</c>-th producer and every <c>n</c>-th receiver, so each slice both takes and puts.
/// With fewer couriers than routes, neighbouring routes are joined into one loop where it fits. The routes go to the city's couriers in (station, slot) order; the last courier stays free for deliveries, and routes beyond the couriers
/// wait (deliveries cover those buildings while the player is checked in).
/// </summary>
public static class Routes
{
    /// <summary>Sets every courier's route that changed, and returns what the routes put.</summary>
    /// <param name="turn">The turn.</param>
    /// <returns>Every (building, good) some route puts.</returns>
    public static HashSet<(int Building, ItemId Item)> Set(Turn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        var served = new HashSet<(int Building, ItemId Item)>();
        CityState city = turn.City;
        if (!CityQueries.AreRoutesUnlocked(city, turn.Content) || AllAmount(turn.Content) is not { } all)
        {
            return served;
        }

        Courier[] couriers = [.. city.Couriers];
        List<RouteStop[]> routes = Plan(turn, all, Math.Max(0, couriers.Length - 1));
        for (int index = 0; index < couriers.Length - 1 && index < routes.Count; index++)
        {
            (int station, int slot) = (couriers[index].StationId, couriers[index].Slot);
            if (city.CountLines(station, slot) == 0 && !SameRoute(city, station, slot, routes[index]))
            {
                turn.Actions.SetRoute(station, slot, routes[index]);
            }

            if (city.TryGetRoute(station, slot, out Route? route))
            {
                foreach (RouteStop stop in route.Stops)
                {
                    served.UnionWith(stop.Actions.Where(action => action.Kind == RouteActionKind.Put).Select(action => (stop.BuildingId, action.Item)));
                }
            }
        }

        return served;
    }

    /// <summary>
    /// How many routes the city's chain needs with one more producer and one more receiver of every good, after joining (see <see cref="Plan"/>):
    /// what the next chain building would need, so <see cref="Construction.IsRouteCapacityShort"/> can ask for couriers first.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The routes.</returns>
    public static int NeededWithOneMore(Turn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        int maxStops = turn.Content.Rules.MaxRouteStops;
        var sizes = new List<int>();
        foreach ((_, Building[] takers, Building[] receivers) in Flows(turn).Where(flow => flow.Takers.Length > 0 && flow.Receivers.Length > 0))
        {
            int stops = takers.Length + receivers.Length + 2;
            int slices = Slices(stops, maxStops);
            sizes.AddRange(Enumerable.Range(0, slices).Select(slice => (stops / slices) + (slice < stops % slices ? 1 : 0)));
        }

        for (int index = 0; index + 1 < sizes.Count;)
        {
            if (sizes[index] + sizes[index + 1] <= maxStops)
            {
                sizes[index] += sizes[index + 1];
                sizes.RemoveAt(index + 1);
            }
            else
            {
                index++;
            }
        }

        return sizes.Count;
    }

    /// <summary>
    /// Every route the chain wants now, in courier order: one per good slice, then, while there are more routes than <paramref name="capacity"/>,
    /// neighbouring routes joined into one loop when the joined loop still fits the content's stop and action limits (a small city then runs
    /// its whole chain on one courier: Woodcutter, Sawmill, Trading Post).
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="all">The amount every action uses.</param>
    /// <param name="capacity">The couriers that may get a route.</param>
    /// <returns>The routes' stops.</returns>
    private static List<RouteStop[]> Plan(Turn turn, Amount all, int capacity)
    {
        var routes = new List<List<(int Building, List<RouteAction> Actions)>>();
        int maxStops = turn.Content.Rules.MaxRouteStops;
        foreach ((ItemId item, Building[] takers, Building[] receivers) in Flows(turn))
        {
            int slices = Slices(takers.Length + receivers.Length, maxStops);
            for (int slice = 0; slice < slices; slice++)
            {
                Building[] sliceTakers = [.. takers.Where((_, index) => index % slices == slice)];
                Building[] sliceReceivers = [.. receivers.Where((_, index) => index % slices == slice)];
                if (sliceTakers.Length == 0 || sliceReceivers.Length == 0)
                {
                    continue;
                }

                List<(int Building, List<RouteAction> Actions)> stops = Compact(sliceTakers.Select(building => (building.Id, new List<RouteAction> { new(RouteActionKind.Take, item, all) }))
                    .Concat(sliceReceivers.Select(building => (building.Id, new List<RouteAction> { new(RouteActionKind.Put, item, all) }))));
                if (stops.Count >= 2)
                {
                    routes.Add(stops);
                }
            }
        }

        for (int index = 0; routes.Count > capacity && index + 1 < routes.Count;)
        {
            List<(int Building, List<RouteAction> Actions)> joined = Compact(routes[index].Concat(routes[index + 1]));
            if (joined.Count <= maxStops && joined.All(stop => stop.Actions.Count <= turn.Content.Rules.MaxStopActions))
            {
                routes[index] = joined;
                routes.RemoveAt(index + 1);
            }
            else
            {
                index++;
            }
        }

        return [.. routes.Select(route => route.Select(stop => new RouteStop(stop.Building, stop.Actions)).ToArray())];
    }

    /// <summary>
    /// Stops in order with no two in a row at one building (04 §19), the last and the first included because the route loops: such stops
    /// become one, its actions in order. The actions are copied, so the inputs stay unchanged.
    /// </summary>
    /// <param name="stops">The stops.</param>
    /// <returns>The compacted stops.</returns>
    private static List<(int Building, List<RouteAction> Actions)> Compact(IEnumerable<(int Building, List<RouteAction> Actions)> stops)
    {
        var result = new List<(int Building, List<RouteAction> Actions)>();
        foreach ((int building, List<RouteAction> actions) in stops)
        {
            if (result.Count > 0 && result[^1].Building == building)
            {
                result[^1].Actions.AddRange(actions);
            }
            else
            {
                result.Add((building, [.. actions]));
            }
        }

        if (result.Count > 1 && result[0].Building == result[^1].Building)
        {
            result[0].Actions.InsertRange(0, result[^1].Actions);
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    /// <summary>
    /// The goods' flows, raw goods first: per good the chain makes (not the currency), its completed producers (by id), then its receivers:
    /// the projects waiting for it (in <see cref="Logistics.ProjectNeeds"/> order) and its completed consumers (by id), each building once.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The flows.</returns>
    private static List<(ItemId Item, Building[] Takers, Building[] Receivers)> Flows(Turn turn)
    {
        Building[] completed = [.. turn.City.Buildings.Where(building => building.Phase != BuildingPhase.Constructing)];
        List<(Building Target, ItemId Item, long Need)> needs = Logistics.ProjectNeeds(turn);
        var flows = new List<(ItemId Item, Building[] Takers, Building[] Receivers)>();
        foreach (ItemId item in turn.Chain.ChainTypes.SelectMany(ContentChain.Outputs).Where(item => item != turn.Content.Currency).Distinct())
        {
            Building[] takers = [.. completed.Where(building => turn.Chain.IsChainType(building.TypeId)
                && ContentChain.Outputs(turn.DefinitionOf(building)).Contains(item))];
            Building[] receivers = [.. needs.Where(need => need.Item == item).Select(need => need.Target)
                .Concat(completed.Where(building => ContentChain.Inputs(turn.DefinitionOf(building)).Contains(item)))
                .DistinctBy(building => building.Id)];
            flows.Add((item, takers, receivers));
        }

        return flows;
    }

    /// <summary>How many routes hold a number of stops.</summary>
    /// <param name="stops">The stops.</param>
    /// <param name="maxStops">The route limit.</param>
    /// <returns><c>ceil(stops / maxStops)</c>.</returns>
    private static int Slices(int stops, int maxStops) => (stops + maxStops - 1) / maxStops;

    /// <summary>Whether a slot's route already has exactly these stops.</summary>
    /// <param name="city">The city.</param>
    /// <param name="stationId">The station.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="stops">The wanted stops.</param>
    /// <returns>True when nothing changes.</returns>
    private static bool SameRoute(CityState city, int stationId, int slot, RouteStop[] stops) =>
        city.TryGetRoute(stationId, slot, out Route? route) && route.Stops.Count == stops.Length
        && route.Stops.Zip(stops).All(pair => pair.First.BuildingId == pair.Second.BuildingId
            && pair.First.Actions.Count == pair.Second.Actions.Count
            && pair.First.Actions.Zip(pair.Second.Actions).All(actions => actions.First.Kind == actions.Second.Kind
                && actions.First.Item == actions.Second.Item && actions.First.Amount.Equals(actions.Second.Amount)));

    /// <summary>The content's preset with the largest percentage (M0: <c>all</c>), or null when the content has none.</summary>
    /// <param name="content">The game content.</param>
    /// <returns>The amount.</returns>
    private static Amount? AllAmount(GameContent content) =>
        content.AmountPresets.OrderByDescending(preset => preset.Percent).ThenBy(preset => preset.Id.Value, StringComparer.Ordinal)
            .Select(preset => Amount.Preset(preset.Id)).FirstOrDefault();
}
