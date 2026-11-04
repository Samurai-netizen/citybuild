using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Cities;
using CityBuilder.Infrastructure.Content;
using CityBuilder.IntegrationTests.Content;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Contracts.ContractContent;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// <c>GET /api/v1/city</c> against the contract (<c>core-instructions/05-api-contract.md</c>, snapshot) on the raw JSON, over cities built by
/// hand on frozen content (<see cref="ContractContent"/>) in a <see cref="FakeCityStore"/>: the real settle, endpoint and serializer, no
/// database. In memory, no Docker; the same endpoint on PostgreSQL is in <c>CityContractDatabaseTests</c>.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its cities, clock and the frozen content.</param>
public sealed class CitySnapshotContractTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    /// <summary>The server second every hand-built city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the hand-built city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>Another player, whose city must never show in the first one's snapshot.</summary>
    private static readonly Guid _otherPlayerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c");

    /// <summary>The temporary directory for the frozen content file.</summary>
    private readonly ContentFileDirectory _files = new();

    /// <summary>Deletes the frozen content file.</summary>
    public void Dispose() => _files.Dispose();

    /// <summary>
    /// The exact text for a city with a building in every phase and every stall reason and three roads (sorted by column, then row, in the
    /// answer), at the instant it was last settled plus half a second:
    /// nothing elapsed, so nothing changed and nothing was saved, and the server time is shown in whole seconds.
    /// </summary>
    [Fact]
    public async Task Get_CityWithABuildingInEveryPhase_ReturnsExactlyTheContractText()
    {
        FakeCityStore store = StoreWith(EveryPhaseCity(_playerId, stateVersion: 7));
        await using WebApplicationFactory<Program> server = store.Serve(factory, new FakeTimeProvider(_settledAt.AddMilliseconds(500)), _files.Write(Json));

        ApiResponse response = await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(JsonNode.Parse(_everyPhaseResponse)!.ToJsonString(), response.Body);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// Every object of the snapshot has exactly the contract's property names and JSON types, a null only where the contract writes one
    /// (the request id of a read; the running research when the slot is free; the end of an idle or frozen building; no stall reason).
    /// </summary>
    [Fact]
    public async Task Get_EveryObject_HasExactlyTheContractNamesAndTypes()
    {
        FakeCityStore store = StoreWith(EveryPhaseCity(_playerId, stateVersion: 7));
        await using WebApplicationFactory<Program> server = store.Serve(factory, new FakeTimeProvider(_settledAt), _files.Write(Json));

        JsonNode snapshot = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();

        JsonShape.AssertObject(snapshot, "requestId:string? stateVersion:int serverTimeUtc:string contentVersion:string city:object settlement:object", "$");
        JsonNode city = snapshot["city"]!;
        JsonShape.AssertObject(
            city,
            "clock:int era:int purse:int totals:object workers:object builders:object research:object buildings:array roads:array trips:array " +
            "couriers:array routes:array",
            "$.city");
        JsonShape.AssertObject(city["workers"], "provided:int assigned:int onTrips:int free:int", "$.city.workers");
        JsonShape.AssertObject(city["builders"], "slots:int busy:int", "$.city.builders");
        JsonShape.AssertObject(city["research"], "activeId:string? startedAt:int? endsAt:int? needs:object completed:array", "$.city.research");
        JsonArray buildings = city["buildings"]!.AsArray();
        Assert.Equal(10, buildings.Count);
        for (int index = 0; index < buildings.Count; index++)
        {
            JsonShape.AssertObject(
                buildings[index],
                "id:int typeId:string x:int y:int level:int assignedWorkers:int maxWorkers:int phase:string phaseStartedAt:int phaseEndsAt:int? " +
                "cycleLabor:int laborRemaining:int segmentStartedAt:int stallReason:string? store:object storeLimits:object needs:object project:string?",
                $"$.city.buildings[{index}]");
        }

        JsonArray trips = city["trips"]!.AsArray();
        Assert.Single(trips);
        JsonShape.AssertObject(trips[0], "id:int sourceId:int targetId:int itemId:string quantity:int departedAt:int arrivesAt:int", "$.city.trips[0]");

        JsonArray couriers = city["couriers"]!.AsArray();
        Assert.Equal(2, couriers.Count);
        for (int index = 0; index < couriers.Count; index++)
        {
            JsonShape.AssertObject(
                couriers[index],
                "stationId:int slot:int state:string toStop:int departedAt:int? arrivesAt:int? stamina:int load:array",
                $"$.city.couriers[{index}]");
            JsonShape.AssertObject(couriers[index]!["load"]![0], "itemId:string fromId:int quantity:int", $"$.city.couriers[{index}].load[0]");
        }

        JsonArray routes = city["routes"]!.AsArray();
        Assert.Equal(2, routes.Count);
        for (int index = 0; index < routes.Count; index++)
        {
            JsonShape.AssertObject(routes[index], "stationId:int slot:int stops:array", $"$.city.routes[{index}]");
            JsonShape.AssertObject(routes[index]!["stops"]![0], "buildingId:int actions:array", $"$.city.routes[{index}].stops[0]");
            JsonShape.AssertObject(routes[index]!["stops"]![0]!["actions"]![0], "kind:string itemId:string amount:string", $"$.city.routes[{index}].stops[0].actions[0]");
        }

        JsonArray roads = city["roads"]!.AsArray();
        Assert.Equal(3, roads.Count);
        for (int index = 0; index < roads.Count; index++)
        {
            JsonShape.AssertObject(roads[index], "x:int y:int level:int", $"$.city.roads[{index}]");
        }

        JsonShape.AssertObject(
            snapshot["settlement"],
            "elapsedSeconds:int produced:object consumed:object delivered:object moved:object refunded:object courierSeconds:int " +
            "tiredCouriers:array completedBuildings:array completedResearch:array eraChanged:bool",
            "$.settlement");
        Assert.Equal(buildings.Select(building => (int)building!["id"]!).Order(), buildings.Select(building => (int)building!["id"]!));
    }

    /// <summary>
    /// A new city (the content's starting city, nothing built) has empty building and road lists and a free research slot written as nulls.
    /// </summary>
    [Fact]
    public async Task Get_StarterCity_HasNoBuildingsAndAFreeResearchSlot()
    {
        string contentPath = _files.Write(Json);
        var store = new FakeCityStore();
        store.Add(new CityAggregate(
            Guid.NewGuid(),
            _playerId,
            CityFactory.CreateStarterCity(ContentFileLoader.Load(contentPath)),
            [],
            _settledAt.ToUnixTimeSeconds(),
            stateVersion: 1,
            "contract-2"));
        await using WebApplicationFactory<Program> server = store.Serve(factory, new FakeTimeProvider(_settledAt), contentPath);

        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json()["city"]!;

        // A new city has coins only (CR-0008 D2).
        Assert.Equal(300, (int)city["purse"]!);
        Assert.Equal("""{"coins":300,"logs":0,"planks":0}""", city["totals"]!.ToJsonString());
        Assert.Equal("""{"activeId":null,"startedAt":null,"endsAt":null,"needs":{},"completed":[]}""", city["research"]!.ToJsonString());
        Assert.Equal("[]", city["buildings"]!.ToJsonString());
        Assert.Equal("[]", city["roads"]!.ToJsonString());
        Assert.Equal("[]", city["trips"]!.ToJsonString());
        Assert.Equal("""{"provided":0,"assigned":0,"onTrips":0,"free":0}""", city["workers"]!.ToJsonString());
        Assert.Equal("""{"slots":2,"busy":0}""", city["builders"]!.ToJsonString());
    }

    /// <summary>
    /// 100 seconds after the last settlement the request settles the city: the clock and stock moved, the settlement block says what happened,
    /// the new state is saved with the next version and the ledger row, and the server time is the clock's.
    /// </summary>
    [Fact]
    public async Task Get_AfterTimeHasPassed_SettlesAndSavesAndReportsTheSettlement()
    {
        FakeCityStore store = StoreWith(ProducingCity(_playerId));
        var clock = new FakeTimeProvider(_settledAt);
        await using WebApplicationFactory<Program> server = store.Serve(factory, clock, _files.Write(Json));
        clock.Advance(TimeSpan.FromSeconds(100));

        JsonNode snapshot = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();

        Assert.Equal(1, (int)snapshot["stateVersion"]!);
        Assert.Equal("2026-10-26T12:01:40Z", (string)snapshot["serverTimeUtc"]!);
        Assert.Equal(1100, (int)snapshot["city"]!["clock"]!);
        // The 15-second cycles end at 1005, 1020, ..., 1095: seven logs into the woodcutter's store; the next cycle started at 1095 and ends at 1110.
        Assert.Equal("""{"coins":100,"logs":17,"planks":0}""", snapshot["city"]!["totals"]!.ToJsonString());
        Assert.Equal("""{"logs":17}""", snapshot["city"]!["buildings"]![1]!["store"]!.ToJsonString());
        Assert.Equal(
            """{"elapsedSeconds":100,"produced":{"logs":7},"consumed":{},"delivered":{},"moved":{},"refunded":{},"courierSeconds":0,"tiredCouriers":[],"completedBuildings":[],"completedResearch":[],"eraChanged":false}""",
            snapshot["settlement"]!.ToJsonString());
        JsonNode woodcutter = snapshot["city"]!["buildings"]![1]!;
        Assert.Equal((2, "Producing", 1095, 1110, 30), ((int)woodcutter["id"]!, (string)woodcutter["phase"]!, (int)woodcutter["phaseStartedAt"]!, (int)woodcutter["phaseEndsAt"]!, (int)woodcutter["laborRemaining"]!));
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, store.Stored(_playerId).StateVersion);
        Assert.Equal((7, "logs"), (Assert.Single(store.Ledger).Delta, Assert.Single(store.Ledger).Item.Value));
    }

    /// <summary>
    /// A second request in the same second changes nothing: the same city and version, an empty settlement, nothing saved. A request a second
    /// later moves the clock, so it is saved again with the next version.
    /// </summary>
    [Fact]
    public async Task Get_TwiceInTheSameSecond_SavesOnlyTheFirstAndKeepsTheVersion()
    {
        FakeCityStore store = StoreWith(ProducingCity(_playerId));
        var clock = new FakeTimeProvider(_settledAt);
        await using WebApplicationFactory<Program> server = store.Serve(factory, clock, _files.Write(Json));
        clock.Advance(TimeSpan.FromSeconds(100));

        JsonNode first = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        JsonNode second = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        clock.Advance(TimeSpan.FromSeconds(1));
        JsonNode third = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();

        Assert.Equal(
            (1, 1, 2),
            ((int)first["stateVersion"]!, (int)second["stateVersion"]!, (int)third["stateVersion"]!));
        Assert.Equal(first["city"]!.ToJsonString(), second["city"]!.ToJsonString());
        Assert.Equal(
            """{"elapsedSeconds":0,"produced":{},"consumed":{},"delivered":{},"moved":{},"refunded":{},"courierSeconds":0,"tiredCouriers":[],"completedBuildings":[],"completedResearch":[],"eraChanged":false}""",
            second["settlement"]!.ToJsonString());
        Assert.Equal(1101, (int)third["city"]!["clock"]!);
        Assert.Equal(2, store.SaveCount);
    }

    /// <summary>
    /// The city comes only from the token: another player's city never shows, and a player id in the query string is ignored.
    /// </summary>
    [Fact]
    public async Task Security_OtherPlayersCity_NeverShowsWhateverTheRequestSays()
    {
        FakeCityStore store = StoreWith(EveryPhaseCity(_playerId, stateVersion: 7), ProducingCity(_otherPlayerId));
        await using WebApplicationFactory<Program> server = store.Serve(factory, new FakeTimeProvider(_settledAt), _files.Write(Json));

        JsonNode own = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        JsonNode asked = (await ApiClient.GetAsync(server, $"/api/v1/city?playerId={_otherPlayerId}", _playerId)).Json();
        JsonNode other = (await ApiClient.GetAsync(server, "/api/v1/city", _otherPlayerId)).Json();

        Assert.Equal((10, 10, 2), (own["city"]!["buildings"]!.AsArray().Count, asked["city"]!["buildings"]!.AsArray().Count, other["city"]!["buildings"]!.AsArray().Count));
        Assert.Equal(own.ToJsonString(), asked.ToJsonString());
    }

    /// <summary>
    /// A small producing city on the frozen content at clock 1000: a house (3 workers) and a woodcutter with 2 workers in a 15-second cycle that
    /// started at 990 and ends at 1005, with 10 logs in its store (a limit of 40), and 100 coins. Canonical, last settled at <see cref="_settledAt"/>.
    /// </summary>
    /// <param name="playerId">The owner.</param>
    /// <returns>The city, at state version 0.</returns>
    private static CityAggregate ProducingCity(Guid playerId) => new(
        Guid.NewGuid(),
        playerId,
        new CityState(
            clock: 1000,
            era: 1,
            nextBuildingId: 3,
            purse: 100,
            buildings:
            [
                new Building(1, House, 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(2, Woodcutter, 1, 0, 1, 2, BuildingPhase.Producing, 990, 1005, 0, 30, 30, 990, [new(Logs, 10)]),
            ],
            completedResearch: [],
            activeResearch: null),
        [],
        _settledAt.ToUnixTimeSeconds(),
        stateVersion: 0,
        "contract-2");

    /// <summary>Builds a store with the cities.</summary>
    /// <param name="cities">The cities to store, one per player.</param>
    /// <returns>The store.</returns>
    private static FakeCityStore StoreWith(params CityAggregate[] cities)
    {
        var store = new FakeCityStore();
        foreach (CityAggregate city in cities)
        {
            store.Add(city);
        }

        return store;
    }

    /// <summary>
    /// A city on the frozen content at clock 1000 that is canonical (the settle's closing advance changes nothing), with a building in every
    /// phase and every stall reason, goods in its stores (CR-0008): a house (level 2); a woodcutter producing with 2 workers (12 logs); a level
    /// 2 woodcutter whose cycle is frozen (no workers, 5 logs); a trading post waiting for planks (1 plank, 3 logs); a woodcutter whose store
    /// is full (140 logs: 40 and the storehouses' 100); a woodcutter's construction site waiting for 10 logs; a house being upgraded; the
    /// study; a depot with 4 planks, from which trip 1 carries 2 more planks to the trading post; a post with 2 couriers: 10/1 walks to the
    /// trading post (its route's stop 2) with a plank from the depot, 10/2 rests with 2 logs from woodcutter 2, and both slots have routes
    /// (CR-0008-T1 part G). 150 coins. The research
    /// <c>trade_charter</c> and <c>storehouses</c> are done, <c>sharp_axes</c> is running. Roads at (2, 1) and (2, 2) at level 1 and at
    /// (1, 1) at level 2, given out of order.
    /// </summary>
    /// <param name="playerId">The owner.</param>
    /// <param name="stateVersion">The stored version.</param>
    /// <returns>The city, last settled at <see cref="_settledAt"/>.</returns>
    private static CityAggregate EveryPhaseCity(Guid playerId, long stateVersion)
    {
        var state = new CityState(
            clock: 1000,
            era: 1,
            nextBuildingId: 11,
            purse: 150,
            buildings:
            [
                new Building(1, House, 0, 0, 2, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(2, Woodcutter, 1, 0, 1, 2, BuildingPhase.Producing, 990, 1005, 0, 30, 30, 990, [new(Logs, 12)]),
                new Building(3, Woodcutter, 2, 0, 2, 0, BuildingPhase.Producing, 980, null, 0, 30, 18, 995, [new(Logs, 5)]),
                new Building(4, TradingPost, 3, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(Logs, 3), new(Planks, 1)]),
                new Building(5, Woodcutter, 4, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(Logs, 140)]),
                new Building(6, Woodcutter, 0, 2, 1, 0, BuildingPhase.Constructing, 0, null, 0, 0, 0, 0, null, [new(Logs, 10)]),
                new Building(7, House, 1, 2, 1, 0, BuildingPhase.Upgrading, 995, 1040, 2, 0, 0, 0),
                new Building(8, Study, 3, 2, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(9, Depot, 4, 2, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(Planks, 4)]),
                new Building(10, Post, 0, 4, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            ],
            completedResearch: [TradeCharter, Storehouses],
            activeResearch: new ActiveResearch(SharpAxes, 990, 1110),
            roads: [new RoadTile(2, 1, 1), new RoadTile(1, 1, 2), new RoadTile(2, 2, 1)],
            trips: [new Trip(1, 9, 4, Planks, 2, 995, 1012)],
            nextTripId: 2,
            couriers:
            [
                new Courier(10, 1, CourierState.Walking, 2, 995, 1020, 900, [new CourierLoad(Planks, 9, 1)]),
                new Courier(10, 2, CourierState.Resting, 0, null, null, 0, [new CourierLoad(Logs, 2, 2)]),
            ],
            routes:
            [
                new Route(10, 1, [Stop(9, RouteActionKind.Take, Planks, "half"), Stop(4, RouteActionKind.Put, Planks, "all")]),
                new Route(10, 2, [Stop(2, RouteActionKind.Take, Logs, "6"), Stop(6, RouteActionKind.Put, Logs, "all")]),
            ]);
        return new CityAggregate(
            Guid.NewGuid(),
            playerId,
            state,
            [new(TradeCharter, 400), new(Storehouses, 900)],
            _settledAt.ToUnixTimeSeconds(),
            stateVersion,
            "contract-2");
    }

    /// <summary>A route stop with one action.</summary>
    /// <param name="buildingId">The building.</param>
    /// <param name="kind">Take or put.</param>
    /// <param name="item">The good.</param>
    /// <param name="amount">The amount's text.</param>
    /// <returns>The stop.</returns>
    private static RouteStop Stop(int buildingId, RouteActionKind kind, ItemId item, string amount) =>
        new(buildingId, [new RouteAction(kind, item, Amount.TryParse(amount, out Amount? parsed) ? parsed : throw new ArgumentException(amount))]);

    /// <summary>The contract's text for <see cref="EveryPhaseCity"/>: every value checked by hand against the rules of the engine.</summary>
    private const string _everyPhaseResponse = """
        {
          "requestId": null, "stateVersion": 7, "serverTimeUtc": "2026-10-26T12:00:00Z", "contentVersion": "contract-2",
          "city": {
            "clock": 1000, "era": 1, "purse": 150,
            "totals": {"coins": 150, "logs": 162, "planks": 8},
            "workers": {"provided": 8, "assigned": 6, "onTrips": 1, "free": 1},
            "builders": {"slots": 2, "busy": 2},
            "research": {"activeId": "sharp_axes", "startedAt": 990, "endsAt": 1110, "needs": {}, "completed": ["storehouses", "trade_charter"]},
            "buildings": [
              {"id": 1, "typeId": "house", "x": 0, "y": 0, "level": 2, "assignedWorkers": 0, "maxWorkers": 0, "phase": "Idle", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": "NoRecipe", "store": {}, "storeLimits": {}, "needs": {}, "project": null},
              {"id": 2, "typeId": "woodcutter", "x": 1, "y": 0, "level": 1, "assignedWorkers": 2, "maxWorkers": 2, "phase": "Producing", "phaseStartedAt": 990, "phaseEndsAt": 1005, "cycleLabor": 30, "laborRemaining": 30, "segmentStartedAt": 990, "stallReason": null, "store": {"logs": 12}, "storeLimits": {"logs": 140}, "needs": {}, "project": null},
              {"id": 3, "typeId": "woodcutter", "x": 2, "y": 0, "level": 2, "assignedWorkers": 0, "maxWorkers": 3, "phase": "Producing", "phaseStartedAt": 980, "phaseEndsAt": null, "cycleLabor": 30, "laborRemaining": 18, "segmentStartedAt": 995, "stallReason": "NoWorkers", "store": {"logs": 5}, "storeLimits": {"logs": 160}, "needs": {}, "project": null},
              {"id": 4, "typeId": "trading_post", "x": 3, "y": 0, "level": 1, "assignedWorkers": 1, "maxWorkers": 1, "phase": "Idle", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": "MissingInput:planks", "store": {"logs": 3, "planks": 1}, "storeLimits": {"logs": 110, "planks": 70}, "needs": {}, "project": null},
              {"id": 5, "typeId": "woodcutter", "x": 4, "y": 0, "level": 1, "assignedWorkers": 1, "maxWorkers": 2, "phase": "Idle", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": "StorageFull:logs", "store": {"logs": 140}, "storeLimits": {"logs": 140}, "needs": {}, "project": null},
              {"id": 6, "typeId": "woodcutter", "x": 0, "y": 2, "level": 1, "assignedWorkers": 0, "maxWorkers": 2, "phase": "Constructing", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": null, "store": {}, "storeLimits": {"logs": 140}, "needs": {"logs": 10}, "project": "Construction"},
              {"id": 7, "typeId": "house", "x": 1, "y": 2, "level": 1, "assignedWorkers": 0, "maxWorkers": 0, "phase": "Upgrading", "phaseStartedAt": 995, "phaseEndsAt": 1040, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": null, "store": {}, "storeLimits": {}, "needs": {}, "project": null},
              {"id": 8, "typeId": "study", "x": 3, "y": 2, "level": 1, "assignedWorkers": 0, "maxWorkers": 0, "phase": "Idle", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": "NoRecipe", "store": {}, "storeLimits": {}, "needs": {}, "project": null},
              {"id": 9, "typeId": "depot", "x": 4, "y": 2, "level": 1, "assignedWorkers": 0, "maxWorkers": 0, "phase": "Idle", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": "NoRecipe", "store": {"planks": 4}, "storeLimits": {"logs": 200, "planks": 150}, "needs": {}, "project": null},
              {"id": 10, "typeId": "post", "x": 0, "y": 4, "level": 1, "assignedWorkers": 2, "maxWorkers": 2, "phase": "Idle", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": null, "store": {}, "storeLimits": {}, "needs": {}, "project": null}
            ],
            "roads": [{"x": 1, "y": 1, "level": 2}, {"x": 2, "y": 1, "level": 1}, {"x": 2, "y": 2, "level": 1}],
            "trips": [{"id": 1, "sourceId": 9, "targetId": 4, "itemId": "planks", "quantity": 2, "departedAt": 995, "arrivesAt": 1012}],
            "couriers": [
              {"stationId": 10, "slot": 1, "state": "Walking", "toStop": 2, "departedAt": 995, "arrivesAt": 1020, "stamina": 900, "load": [{"itemId": "planks", "fromId": 9, "quantity": 1}]},
              {"stationId": 10, "slot": 2, "state": "Resting", "toStop": 0, "departedAt": null, "arrivesAt": null, "stamina": 0, "load": [{"itemId": "logs", "fromId": 2, "quantity": 2}]}
            ],
            "routes": [
              {"stationId": 10, "slot": 1, "stops": [{"buildingId": 9, "actions": [{"kind": "take", "itemId": "planks", "amount": "half"}]}, {"buildingId": 4, "actions": [{"kind": "put", "itemId": "planks", "amount": "all"}]}]},
              {"stationId": 10, "slot": 2, "stops": [{"buildingId": 2, "actions": [{"kind": "take", "itemId": "logs", "amount": "6"}]}, {"buildingId": 6, "actions": [{"kind": "put", "itemId": "logs", "amount": "all"}]}]}
            ]
          },
          "settlement": {"elapsedSeconds": 0, "produced": {}, "consumed": {}, "delivered": {}, "moved": {}, "refunded": {}, "courierSeconds": 0, "tiredCouriers": [], "completedBuildings": [], "completedResearch": [], "eraChanged": false}
        }
        """;
}
