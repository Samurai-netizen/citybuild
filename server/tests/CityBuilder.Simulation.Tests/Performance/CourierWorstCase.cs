using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Golden;

namespace CityBuilder.Simulation.Tests.Performance;

/// <summary>
/// The courier worst case of the performance budget (<c>04</c> section 12, CR-0008-T1 part I) over the game's content (the frozen
/// <see cref="GoldenContent.V2"/>, which is <c>mvp-2</c>): the 8 × 8 grid filled with 26 houses at level 2 (6 workers each, 156) and 38
/// courier stations at level 2 (4 couriers each, 152, with 28 800 s of stamina), Sturdy Boots done, so a walk to a neighbouring tile takes
/// 4 s. Every courier loops between its station and the neighbouring building to its right (to its left in the last column): the shortest
/// walk there is, taking all of the neighbour's logs and putting them into the station. The city is restored as it is stored, at clock 1000
/// with every courier at its station; the first 38 tiles row by row are the stations, the other 26 the houses. The cost is the
/// couriers' arrivals: one settlement with the player away from 300 s runs each courier until its stamina is gone, about 7 300 arrivals
/// each, about 1.1 million in all.
/// </summary>
internal static class CourierWorstCase
{
    /// <summary>The city clock the city is stored at.</summary>
    public const long Clock = 1000;

    /// <summary>The courier stations of the case.</summary>
    public const int Stations = 38;

    /// <summary>The couriers of a level-2 station.</summary>
    public const int CouriersPerStation = 4;

    /// <summary>A level-2 station's stamina, in seconds.</summary>
    public const long Stamina = 28_800;

    /// <summary>The presence grace of the server, in seconds: the absence starts this long after the clock.</summary>
    public const long Grace = 300;

    /// <summary>A walk to a neighbouring tile with Sturdy Boots (5 000 ms less 20 %), in seconds.</summary>
    public const long Step = 4;

    /// <summary>The content of the case: the game's.</summary>
    public static GameContent CaseContent => GoldenContent.V2;

    /// <summary>The city of the class summary, a new object each time.</summary>
    public static CityState Build()
    {
        var buildings = new List<Building>();
        var couriers = new List<Courier>();
        var routes = new List<Route>();
        int width = CaseContent.Rules.GridWidth;
        int height = CaseContent.Rules.GridHeight;
        int stations = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // The first 38 tiles, row by row, are stations and the rest houses; a neighbour may be a station or a house.
                int id = (y * width) + x + 1;
                bool station = stations < Stations;
                buildings.Add(new Building(
                    id, new BuildingTypeId(station ? "courier_station" : "house"), x, y, 2, station ? CouriersPerStation : 0,
                    BuildingPhase.Idle, 0, null, 0, 0, 0, 0));
                if (!station)
                {
                    continue;
                }

                stations++;
                int neighbour = x + 1 < width ? id + 1 : id - 1;
                for (int slot = 1; slot <= CouriersPerStation; slot++)
                {
                    couriers.Add(new Courier(id, slot, CourierState.AtStation, 0, null, null, Stamina));
                    routes.Add(new Route(id, slot,
                    [
                        new RouteStop(neighbour, [new RouteAction(RouteActionKind.Take, new ItemId("logs"), All)]),
                        new RouteStop(id, [new RouteAction(RouteActionKind.Put, new ItemId("logs"), All)]),
                    ]));
                }
            }
        }

        return new CityState(
            Clock, 1, buildings.Count + 1, 0, buildings, [new ResearchId("sturdy_boots")], activeResearch: null, couriers: couriers, routes: routes);
    }

    /// <summary>The preset <c>all</c>.</summary>
    private static Amount All => Amount.Preset(new AmountPresetId("all"));
}
