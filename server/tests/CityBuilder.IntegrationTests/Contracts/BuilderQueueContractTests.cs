using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Cities;
using CityBuilder.IntegrationTests.Content;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Contracts.ContractContent;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// The builder queue over HTTP (CR-0013, <c>05</c>): with both builders busy, <c>place-building</c> and <c>upgrade-building</c> answer 200 and
/// queue the order; with the queue full they answer <c>NO_FREE_BUILDER</c>. Hand-built cities on the frozen <see cref="ContractContent"/>
/// (two builders, a queue of two) in a <see cref="FakeCityStore"/>: the real pipeline, engine and serializer, no database.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city, clock and the frozen content.</param>
public sealed class BuilderQueueContractTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    /// <summary>The server second every hand-built city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the hand-built city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>The temporary directory for the frozen content file.</summary>
    private readonly ContentFileDirectory _files = new();

    /// <summary>Deletes the frozen content file.</summary>
    public void Dispose() => _files.Dispose();

    /// <summary>A placement with both builders busy pays its 40 coins and answers with the woodcutter queued in place 1.</summary>
    [Fact]
    public async Task PostPlaceBuilding_EveryBuilderBusy_QueuesTheConstruction()
    {
        await using WebApplicationFactory<Program> server = Serve(BusyCity());

        ApiResponse response = await ApiClient.PostAsync(
            server, "/api/v1/commands/place-building", _playerId, """{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000d01","buildingTypeId":"woodcutter","x":4,"y":0}""");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode city = response.Json()["city"]!;
        Assert.Equal(60, (long)city["purse"]!);
        Assert.Equal("""{"slots":2,"busy":2,"queued":1,"queueLength":2}""", city["builders"]!.ToJsonString());
        JsonNode queued = city["buildings"]!.AsArray().Single(building => (int)building!["id"]! == 4)!;
        Assert.Equal(("Queued", 1), ((string)queued["phase"]!, (int)queued["queuePlace"]!));
    }

    /// <summary>An upgrade with both builders busy pays its 30 coins and answers with the house still idle, its upgrade queued in place 1.</summary>
    [Fact]
    public async Task PostUpgradeBuilding_EveryBuilderBusy_QueuesTheUpgrade()
    {
        await using WebApplicationFactory<Program> server = Serve(BusyCity());

        ApiResponse response = await ApiClient.PostAsync(
            server, "/api/v1/commands/upgrade-building", _playerId, """{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000d02","buildingId":1}""");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode city = response.Json()["city"]!;
        JsonNode house = city["buildings"]![0]!;
        Assert.Equal((70L, "Idle", 1, 1), ((long)city["purse"]!, (string)house["phase"]!, (int)house["level"]!, (int)house["queuePlace"]!));
        Assert.Equal("""{"slots":2,"busy":2,"queued":1,"queueLength":2}""", city["builders"]!.ToJsonString());
    }

    /// <summary>With the queue full a placement is <c>NO_FREE_BUILDER</c> (409) and nothing is saved.</summary>
    [Fact]
    public async Task PostPlaceBuilding_QueueFull_Is409NoFreeBuilder()
    {
        CityAggregate city = BusyCity(
            new Building(4, Woodcutter, 4, 0, 1, 0, BuildingPhase.Queued, 0, null, 0, 0, 0, 0, queuedOrder: 1),
            new Building(5, Woodcutter, 5, 0, 1, 0, BuildingPhase.Queued, 0, null, 0, 0, 0, 0, queuedOrder: 2));
        FakeCityStore store = Store(city);
        await using WebApplicationFactory<Program> server = store.Serve(factory, new FakeTimeProvider(_settledAt), _files.Write(Json));

        ApiResponse response = await ApiClient.PostAsync(
            server, "/api/v1/commands/place-building", _playerId, """{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000d03","buildingTypeId":"woodcutter","x":0,"y":1}""");

        Assert.Equal((HttpStatusCode.Conflict, "NO_FREE_BUILDER"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// A city at clock 1000 with 100 coins, an idle house (level 1; its level 2 costs 30 coins and 15 logs, the coins paid at the order) and
    /// two woodcutters under construction until 1050 and 1060, which keep both builders busy; <paramref name="more"/> buildings are added.
    /// </summary>
    private static CityAggregate BusyCity(params Building[] more)
    {
        Building[] buildings =
        [
            new(1, House, 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, Woodcutter, 1, 0, 1, 0, BuildingPhase.Constructing, 990, 1050, 0, 0, 0, 0),
            new(3, Woodcutter, 2, 0, 1, 0, BuildingPhase.Constructing, 990, 1060, 0, 0, 0, 0),
            .. more,
        ];
        var state = new CityState(
            1000, 1, buildings.Max(building => building.Id) + 1, 100, buildings, [], activeResearch: null,
            nextQueuedOrder: buildings.Max(building => building.QueuedOrder) + 1);
        return new CityAggregate(Guid.NewGuid(), _playerId, state, [], _settledAt.ToUnixTimeSeconds(), stateVersion: 3, "contract-2");
    }

    /// <summary>A store holding the one city.</summary>
    private static FakeCityStore Store(CityAggregate city)
    {
        var store = new FakeCityStore();
        store.Add(city);
        return store;
    }

    /// <summary>The server over a store with the city, at the second it was last settled, on the frozen content.</summary>
    private WebApplicationFactory<Program> Serve(CityAggregate city) => Store(city).Serve(factory, new FakeTimeProvider(_settledAt), _files.Write(Json));
}
