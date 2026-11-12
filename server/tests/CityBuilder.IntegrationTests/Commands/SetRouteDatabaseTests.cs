using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.State;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// <c>POST /api/v1/commands/set-route</c> end to end on PostgreSQL over HTTP (CR-0008-T1 part G): the server's real content (a courier station
/// level 1 takes 2 couriers carrying 10), endpoint, pipeline and repositories, the server clock a <c>FakeTimeProvider</c> at
/// <see cref="PostgresApiFactory.Start"/>. Each test stores a city built by hand at clock 0 (house 1 at (0,0), woodcutter 2 at (1,1) with 30
/// logs, warehouse 3 at (1,3), courier station 4 at (3,3)), staffs the station over HTTP and checks the stored rows: <c>city_couriers</c>,
/// <c>city_courier_loads</c>, <c>city_route_actions</c>, the stores, the ledger and the receipts.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class SetRouteDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/set-route";

    /// <summary>The route of the tests: all the woodcutter's logs into the warehouse.</summary>
    private const string _woodToWarehouse =
        """[{"buildingId":2,"actions":[{"kind":"take","itemId":"logs","amount":"all"}]},{"buildingId":3,"actions":[{"kind":"put","itemId":"logs","amount":"all"}]}]""";

    /// <summary>The request id of the route the tests set.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000004b1");

    /// <summary>
    /// The route is stored as one action row per action (stops and actions from 0, the amount's text), courier 4/1 as walking to stop 1 from
    /// clock 0; nothing goes to the ledger and the receipt holds the response. When it reaches the woodcutter, the settle of the next request
    /// stores its load (10 logs from building 2) and the woodcutter's store holds 20; the city's total stays 30 and no ledger row is written.
    /// </summary>
    [Fact]
    public async Task Post_Route_StoresItThenTheCouriersLoad()
    {
        (Guid player, Guid city) = await StaffedCityAsync("router");

        ApiResponse response = await RouteAsync(player, _requestId, _woodToWarehouse);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        long arrivesAt = (long)response.Json()["city"]!["couriers"]![0]!["arrivesAt"]!;
        await using (CityBuilderDbContext db = Database.CreateContext())
        {
            Assert.Equal(
                ["4/1 0.0 2 take logs all", "4/1 1.0 3 put logs all"],
                (await db.CityRouteActions.AsNoTracking().OrderBy(row => row.StopIndex).ThenBy(row => row.ActionIndex).ToListAsync(TestContext.Current.CancellationToken))
                    .Select(row => $"{row.StationId}/{row.Slot} {row.StopIndex}.{row.ActionIndex} {row.BuildingId} {row.Kind} {row.ItemId} {row.Amount}"));
            CityCourierRow courier = await db.CityCouriers.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(("Walking", (short)1, (long?)0, (long?)arrivesAt, 14_400L), (courier.State, courier.ToStop, courier.DepartedAt, courier.ArrivesAt, courier.StaminaSeconds));
        }

        Assert.Empty(await Persistence.DatabaseCities.LedgerAsync(Database, city));
        Assert.Contains(await ReceiptsAsync(Database, player), receipt => receipt.RequestId == _requestId);

        Factory.Clock.Advance(TimeSpan.FromSeconds(arrivesAt));
        JsonNode settled = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal("""{"logs":20}""", settled["city"]!["buildings"]![1]!["store"]!.ToJsonString());
        await using (CityBuilderDbContext db = Database.CreateContext())
        {
            CityCourierLoadRow load = await db.CityCourierLoads.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal((4, (short)1, "logs", 2, 10L), (load.StationId, load.Slot, load.ItemId, load.FromBuildingId, load.Quantity));
        }

        Assert.Equal("coins 1000, logs 30", await StockAsync(Database, city));
        Assert.Empty(await Persistence.DatabaseCities.LedgerAsync(Database, city));
    }

    /// <summary>
    /// No stops clear the route: its action rows are deleted, the courier with its load stops, the load row goes and its 10 logs are back in
    /// the woodcutter's store, the courier row says it stands at the station.
    /// </summary>
    [Fact]
    public async Task Post_NoStops_DeletesTheRouteAndGivesTheLoadBack()
    {
        (Guid player, Guid city) = await StaffedCityAsync("clearer");
        ApiResponse first = await RouteAsync(player, _requestId, _woodToWarehouse);
        Factory.Clock.Advance(TimeSpan.FromSeconds((long)first.Json()["city"]!["couriers"]![0]!["arrivesAt"]!));

        ApiResponse response = await RouteAsync(player, new Guid("1de9e700-0000-4000-8000-0000000004b2"), "[]");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        await using (CityBuilderDbContext db = Database.CreateContext())
        {
            Assert.False(await db.CityRouteActions.AnyAsync(TestContext.Current.CancellationToken));
            Assert.False(await db.CityCourierLoads.AnyAsync(TestContext.Current.CancellationToken));
            CityCourierRow courier = await db.CityCouriers.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(("AtStation", (short)0, (long?)null, (long?)null), (courier.State, courier.ToStop, courier.DepartedAt, courier.ArrivesAt));
        }

        Assert.Equal("coins 1000, logs 30", await StockAsync(Database, city));
        Assert.Equal("""{"logs":30}""", response.Json()["city"]!["buildings"]![1]!["store"]!.ToJsonString());
    }

    /// <summary>
    /// A retry with the same request id and body answers with the stored bytes and stores nothing more: one receipt for the route, the rows
    /// as the first answer left them.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReplaysTheStoredResponse()
    {
        (Guid player, Guid city) = await StaffedCityAsync("retrier");
        ApiResponse first = await RouteAsync(player, _requestId, _woodToWarehouse);
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse retry = await RouteAsync(player, _requestId, _woodToWarehouse);

        Assert.Equal((HttpStatusCode.OK, first.Body), (retry.Status, retry.Body));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Another player's station id is <c>BUILDING_NOT_FOUND</c> (404), never a sign that it exists: the attacker, whose city has no building
    /// 4, names the owner's station; nothing is stored in either city.
    /// </summary>
    [Fact]
    public async Task Security_AnotherPlayersStationId_Is404AndTouchesNeitherCity()
    {
        (Guid owner, Guid ownerCity) = await StaffedCityAsync("owner");
        (Guid attacker, Guid attackerCity) = await StoreCityAsync(Factory, "attacker", 1000, House(1), Warehouse(2, logs: 0, planks: 0));
        string ownerStored = await StoredAsync(Factory, Database, owner, ownerCity);
        string attackerStored = await StoredAsync(Factory, Database, attacker, attackerCity);

        ApiResponse response = await RouteAsync(attacker, _requestId, _woodToWarehouse);

        Assert.Equal((HttpStatusCode.NotFound, "BUILDING_NOT_FOUND"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(ownerStored, await StoredAsync(Factory, Database, owner, ownerCity));
        Assert.Equal(attackerStored, await StoredAsync(Factory, Database, attacker, attackerCity));
    }

    /// <summary>Stores the test city and gives the courier station one worker over HTTP: courier 4/1 at the station.</summary>
    /// <param name="name">The player's dev name.</param>
    /// <returns>The player and their city.</returns>
    private async Task<(Guid Player, Guid City)> StaffedCityAsync(string name)
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory, name, 1000, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0, logs: 30), Warehouse(3, logs: 0, planks: 0), CourierStation(4),
            Scriptorium(5, x: 6));
        ApiResponse staffed = await ApiClient.PostAsync(
            Factory, "/api/v1/commands/assign-workers", player, """{"requestId":"1de9e700-0000-4000-8000-0000000004b0","buildingId":4,"workers":1}""");
        Assert.Equal(HttpStatusCode.OK, staffed.Status);
        return (player, city);
    }

    /// <summary>Sets the route of station 4's slot 1 over HTTP with the player's token.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="stops">The stops, as JSON.</param>
    /// <returns>The answer.</returns>
    private Task<ApiResponse> RouteAsync(Guid player, Guid requestId, string stops) =>
        ApiClient.PostAsync(Factory, _path, player, $$"""{"requestId":"{{requestId}}","stationId":4,"slot":1,"stops":{{stops}}}""");
}
