using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Cities;
using CityBuilder.Infrastructure.Content;
using CityBuilder.IntegrationTests.Content;
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
    /// The exact text for a city with a building in every phase and every stall reason, at the instant it was last settled plus half a second:
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
            city, "clock:int era:int inventory:object storageCaps:object workers:object builders:object research:object buildings:array", "$.city");
        JsonShape.AssertObject(city["workers"], "provided:int assigned:int free:int", "$.city.workers");
        JsonShape.AssertObject(city["builders"], "slots:int busy:int", "$.city.builders");
        JsonShape.AssertObject(city["research"], "activeId:string? startedAt:int? endsAt:int? completed:array", "$.city.research");
        JsonArray buildings = city["buildings"]!.AsArray();
        Assert.Equal(7, buildings.Count);
        for (int index = 0; index < buildings.Count; index++)
        {
            JsonShape.AssertObject(
                buildings[index],
                "id:int typeId:string x:int y:int level:int assignedWorkers:int maxWorkers:int phase:string phaseStartedAt:int phaseEndsAt:int? " +
                "cycleLabor:int laborRemaining:int segmentStartedAt:int stallReason:string?",
                $"$.city.buildings[{index}]");
        }

        JsonShape.AssertObject(
            snapshot["settlement"],
            "elapsedSeconds:int productionSeconds:int produced:object consumed:object completedBuildings:array completedResearch:array eraChanged:bool",
            "$.settlement");
        Assert.Equal(buildings.Select(building => (int)building!["id"]!).Order(), buildings.Select(building => (int)building!["id"]!));
    }

    /// <summary>A new city (the content's starting city, nothing built) has an empty building list and a free research slot written as nulls.</summary>
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
            "contract-1"));
        await using WebApplicationFactory<Program> server = store.Serve(factory, new FakeTimeProvider(_settledAt), contentPath);

        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json()["city"]!;

        Assert.Equal("""{"coins":300,"logs":60,"planks":20}""", city["inventory"]!.ToJsonString());
        Assert.Equal("""{"activeId":null,"startedAt":null,"endsAt":null,"completed":[]}""", city["research"]!.ToJsonString());
        Assert.Equal("[]", city["buildings"]!.ToJsonString());
        Assert.Equal("""{"provided":0,"assigned":0,"free":0}""", city["workers"]!.ToJsonString());
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
        // The 15-second cycles end at 1005, 1020, ..., 1095: seven logs; the next cycle started at 1095 and ends at 1110.
        Assert.Equal("""{"coins":100,"logs":17}""", snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal(
            """{"elapsedSeconds":100,"productionSeconds":100,"produced":{"logs":7},"consumed":{},"completedBuildings":[],"completedResearch":[],"eraChanged":false}""",
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
            """{"elapsedSeconds":0,"productionSeconds":0,"produced":{},"consumed":{},"completedBuildings":[],"completedResearch":[],"eraChanged":false}""",
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

        Assert.Equal((7, 7, 2), (own["city"]!["buildings"]!.AsArray().Count, asked["city"]!["buildings"]!.AsArray().Count, other["city"]!["buildings"]!.AsArray().Count));
        Assert.Equal(own.ToJsonString(), asked.ToJsonString());
    }

    /// <summary>
    /// A small producing city on the frozen content at clock 1000: a house (3 workers) and a woodcutter with 2 workers in a 15-second cycle that
    /// started at 990 and ends at 1005, 10 logs and 100 coins in stock (cap 400 logs). Canonical, last settled at <see cref="_settledAt"/>.
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
            inventory: [new(Coins, 100), new(Logs, 10)],
            buildings:
            [
                new Building(1, House, 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(2, Woodcutter, 1, 0, 1, 2, BuildingPhase.Producing, 990, 1005, 0, 30, 30, 990),
            ],
            completedResearch: [],
            activeResearch: null),
        [],
        _settledAt.ToUnixTimeSeconds(),
        stateVersion: 0,
        "contract-1");

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
    /// phase and every stall reason: a house (level 2); a woodcutter producing with 2 workers; a level 2 woodcutter whose cycle is frozen
    /// (no workers); a trading post waiting for planks; a woodcutter waiting for room in storage; a woodcutter under construction; a house
    /// being upgraded. The research <c>trade_charter</c> and <c>storehouses</c> are done, <c>sharp_axes</c> is running.
    /// </summary>
    /// <param name="playerId">The owner.</param>
    /// <param name="stateVersion">The stored version.</param>
    /// <returns>The city, last settled at <see cref="_settledAt"/>.</returns>
    private static CityAggregate EveryPhaseCity(Guid playerId, long stateVersion)
    {
        var state = new CityState(
            clock: 1000,
            era: 1,
            nextBuildingId: 8,
            inventory: [new(Coins, 150), new(Logs, 498), new(Planks, 1)],
            buildings:
            [
                new Building(1, House, 0, 0, 2, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(2, Woodcutter, 1, 0, 1, 2, BuildingPhase.Producing, 990, 1005, 0, 30, 30, 990),
                new Building(3, Woodcutter, 2, 0, 2, 0, BuildingPhase.Producing, 980, null, 0, 30, 18, 995),
                new Building(4, TradingPost, 3, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(5, Woodcutter, 4, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(6, Woodcutter, 0, 2, 1, 0, BuildingPhase.Constructing, 990, 1010, 0, 0, 0, 0),
                new Building(7, House, 1, 2, 1, 0, BuildingPhase.Upgrading, 995, 1040, 2, 0, 0, 0),
            ],
            completedResearch: [TradeCharter, Storehouses],
            activeResearch: new ActiveResearch(SharpAxes, 990, 1110));
        return new CityAggregate(
            Guid.NewGuid(),
            playerId,
            state,
            [new(TradeCharter, 400), new(Storehouses, 900)],
            _settledAt.ToUnixTimeSeconds(),
            stateVersion,
            "contract-1");
    }

    /// <summary>The contract's text for <see cref="EveryPhaseCity"/>: every value checked by hand against the rules of the engine.</summary>
    private const string _everyPhaseResponse = """
        {
          "requestId": null, "stateVersion": 7, "serverTimeUtc": "2026-10-26T12:00:00Z", "contentVersion": "contract-1",
          "city": {
            "clock": 1000, "era": 1,
            "inventory": {"coins": 150, "logs": 498, "planks": 1},
            "storageCaps": {"logs": 500, "planks": 350},
            "workers": {"provided": 8, "assigned": 4, "free": 4},
            "builders": {"slots": 2, "busy": 2},
            "research": {"activeId": "sharp_axes", "startedAt": 990, "endsAt": 1110, "completed": ["storehouses", "trade_charter"]},
            "buildings": [
              {"id": 1, "typeId": "house", "x": 0, "y": 0, "level": 2, "assignedWorkers": 0, "maxWorkers": 0, "phase": "Idle", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": "NoRecipe"},
              {"id": 2, "typeId": "woodcutter", "x": 1, "y": 0, "level": 1, "assignedWorkers": 2, "maxWorkers": 2, "phase": "Producing", "phaseStartedAt": 990, "phaseEndsAt": 1005, "cycleLabor": 30, "laborRemaining": 30, "segmentStartedAt": 990, "stallReason": null},
              {"id": 3, "typeId": "woodcutter", "x": 2, "y": 0, "level": 2, "assignedWorkers": 0, "maxWorkers": 3, "phase": "Producing", "phaseStartedAt": 980, "phaseEndsAt": null, "cycleLabor": 30, "laborRemaining": 18, "segmentStartedAt": 995, "stallReason": "NoWorkers"},
              {"id": 4, "typeId": "trading_post", "x": 3, "y": 0, "level": 1, "assignedWorkers": 1, "maxWorkers": 1, "phase": "Idle", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": "MissingInput:planks"},
              {"id": 5, "typeId": "woodcutter", "x": 4, "y": 0, "level": 1, "assignedWorkers": 1, "maxWorkers": 2, "phase": "Idle", "phaseStartedAt": 0, "phaseEndsAt": null, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": "StorageFull:logs"},
              {"id": 6, "typeId": "woodcutter", "x": 0, "y": 2, "level": 1, "assignedWorkers": 0, "maxWorkers": 2, "phase": "Constructing", "phaseStartedAt": 990, "phaseEndsAt": 1010, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": null},
              {"id": 7, "typeId": "house", "x": 1, "y": 2, "level": 1, "assignedWorkers": 0, "maxWorkers": 0, "phase": "Upgrading", "phaseStartedAt": 995, "phaseEndsAt": 1040, "cycleLabor": 0, "laborRemaining": 0, "segmentStartedAt": 0, "stallReason": null}
            ]
          },
          "settlement": {"elapsedSeconds": 0, "productionSeconds": 0, "produced": {}, "consumed": {}, "completedBuildings": [], "completedResearch": [], "eraChanged": false}
        }
        """;
}
