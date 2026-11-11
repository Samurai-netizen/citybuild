using System.Net;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.State;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// Courier deliveries end to end on PostgreSQL over HTTP (CR-0008 amendment 1): <c>start-trip</c> in a city with a staffed courier station
/// stores the courier's delivery lines and <c>cancel-delivery</c> deletes them, on the server's real content (a courier station level 1
/// carries 10), endpoint, pipeline and repositories. The city is built by hand at clock 0: house 1 at (0,0), woodcutter 2 at (1,1) with 30
/// logs, warehouse 3 at (1,3), courier station 4 at (3,3), staffed over HTTP. It checks the stored <c>city_delivery_lines</c> rows and
/// <c>cities.next_line_id</c>.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CancelDeliveryDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>
    /// A delivery of all 30 logs is stored as three line rows of 10 for courier 4/1 with the next line id 4; cancelling line 2 deletes its row,
    /// and "Cancel all" deletes the rest; the woodcutter's logs never left its store while no courier picked them up.
    /// </summary>
    [Fact]
    public async Task Post_LinesStoredThenCancelled_AreRowsThatGo()
    {
        (Guid player, Guid _) = await StoreCityAsync(
            Factory, "deliverer", 1000, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0, logs: 30), Warehouse(3, logs: 0, planks: 0), CourierStation(4));
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(player, "assign-workers", """{"buildingId":4,"workers":1}""")).Status);

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(player, "start-trip", """{"sourceId":2,"targetId":3,"itemId":"logs","amount":"all"}""")).Status);
        Assert.Equal(["1 4/1 2->3 logs 10", "2 4/1 2->3 logs 10", "3 4/1 2->3 logs 10"], await LinesAsync());
        await using (CityBuilderDbContext db = Database.CreateContext())
        {
            Assert.Equal(4, (await db.Cities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).NextLineId);
        }

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(player, "cancel-delivery", """{"stationId":4,"slot":1,"lineId":2}""")).Status);
        Assert.Equal(["1 4/1 2->3 logs 10", "3 4/1 2->3 logs 10"], await LinesAsync());

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(player, "cancel-delivery", """{"stationId":4,"slot":1,"lineId":null}""")).Status);
        Assert.Empty(await LinesAsync());
    }

    /// <summary>Sends a command with a new request id over HTTP; <paramref name="fields"/> is the body's JSON without its request id.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="command">The command's path segment.</param>
    /// <param name="fields">The body's other fields, as a JSON object.</param>
    /// <returns>The answer.</returns>
    private Task<ApiResponse> PostAsync(Guid player, string command, string fields) =>
        ApiClient.PostAsync(Factory, "/api/v1/commands/" + command, player, $$"""{"requestId":"{{Guid.CreateVersion7()}}",""" + fields[1..]);

    /// <summary>The stored delivery lines, by line id, as <c>id station/slot source->target item quantity</c>.</summary>
    /// <returns>The lines.</returns>
    private async Task<List<string>> LinesAsync()
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityDeliveryLineRow> rows = await db.CityDeliveryLines.AsNoTracking().OrderBy(row => row.LineId).ToListAsync(TestContext.Current.CancellationToken);
        return [.. rows.Select(row => $"{row.LineId} {row.StationId}/{row.Slot} {row.SourceId}->{row.TargetId} {row.ItemId} {row.Quantity}")];
    }
}
