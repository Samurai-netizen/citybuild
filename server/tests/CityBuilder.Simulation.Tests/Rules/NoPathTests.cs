using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// New walks need a path (<c>04</c> sections 8 and 17, M1-03): a trip, a delivery line or a route whose buildings no walk joins (water
/// without a bridge, land under mist) is refused with <see cref="MutationErrorCode.NoPath"/>; a bridge makes the same order fine. The city
/// stands on the Era I map with area C open: a Cottage (5, 5), a Woodcutter's Hut with 20 logs (7, 9), a Warehouse on the east bank
/// (14, 3), the Scriptorium (3, 8); the river is x = 12.
/// </summary>
public sealed class NoPathTests
{
    /// <summary>The server's Era I content, with the map.</summary>
    private static readonly GameContent _content = ContentAssert.Valid(ContentFile.ReadServer("era1-content.json"));

    /// <summary>A trip (carried by a worker, no courier station) across the river without a bridge is refused.</summary>
    [Fact]
    public void StartTrip_AcrossTheRiverWithoutBridge_IsNoPath() =>
        RulesAssert.Rejected(MutationErrorCode.NoPath, Town(), city => Trip(city, 2, 3));

    /// <summary>With a bridge the same trip starts.</summary>
    [Fact]
    public void StartTrip_AcrossTheRiverOnABridge_Starts() => RulesAssert.Ok(Trip(Town(bridge: true), 2, 3));

    /// <summary>A delivery line (a courier carries it) across the river without a bridge is refused.</summary>
    [Fact]
    public void StartTrip_DeliveryLineAcrossTheRiver_IsNoPath() =>
        RulesAssert.Rejected(MutationErrorCode.NoPath, Town(station: true), city => Trip(city, 2, 3));

    /// <summary>With a bridge the delivery line is queued.</summary>
    [Fact]
    public void StartTrip_DeliveryLineOnABridge_IsQueued() => RulesAssert.Ok(Trip(Town(station: true, bridge: true), 2, 3));

    /// <summary>A route whose stops no walk joins is refused.</summary>
    [Fact]
    public void SetRoute_StopsAcrossTheRiverWithoutBridge_IsNoPath() =>
        RulesAssert.Rejected(MutationErrorCode.NoPath, Town(station: true), city => Route(city));

    /// <summary>With a bridge the route is set.</summary>
    [Fact]
    public void SetRoute_StopsAcrossTheRiverOnABridge_IsSet() => RulesAssert.Ok(Route(Town(station: true, bridge: true)));

    /// <summary>
    /// The town of the class summary; with <paramref name="station"/> also a Courier Station (6) at (4, 6) with one courier, with
    /// <paramref name="bridge"/> a bridge at (12, 5).
    /// </summary>
    private static CityState Town(bool station = false, bool bridge = false)
    {
        var buildings = new List<Building>
        {
            Idle(1, "house", 5, 5),
            Idle(2, "woodcutter", 7, 9, store: [("logs", 20)]),
            Idle(3, "warehouse", 14, 3),
            Idle(4, "scriptorium", 3, 8),
        };
        if (station)
        {
            buildings.Add(Idle(6, "courier_station", 4, 6, workers: 1));
        }

        return City(
            purse: 1000,
            buildings: [.. buildings],
            completedResearch: ["bridge_building", "open_fields", "stone_cutting", "trade_charter"],
            roads: bridge ? [new RoadTile(12, 5, 1, isBridge: true)] : null,
            couriers: station ? [AtStation(6, 1)] : null);
    }

    /// <summary>Starts a trip of all the logs from one building to another.</summary>
    private static MutationResult Trip(CityState city, int sourceId, int targetId) =>
        TripRules.StartTrip(city, _content, sourceId, targetId, new ItemId("logs"), Amount.Preset(new AmountPresetId("all")));

    /// <summary>Sets the station's route: take all logs at the Woodcutter's Hut, put them into the Warehouse.</summary>
    private static MutationResult Route(CityState city) =>
        CourierRules.SetRoute(city, _content, 6, 1,
        [
            new RouteStop(2, [new RouteAction(RouteActionKind.Take, new ItemId("logs"), Amount.Preset(new AmountPresetId("all")))]),
            new RouteStop(3, [new RouteAction(RouteActionKind.Put, new ItemId("logs"), Amount.Preset(new AmountPresetId("all")))]),
        ]);
}
