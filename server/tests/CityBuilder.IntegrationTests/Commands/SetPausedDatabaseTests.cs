using System.Net;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.State;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// Pausing end to end on PostgreSQL over HTTP (CR-0008 amendment 2, D23): <c>set-paused</c> stores <c>city_buildings.paused</c>, a paused
/// woodcutter starts no new cycle in later requests, and resuming clears the column, on the server's real content, endpoint, pipeline and
/// repositories. The city is built by hand at clock 0: house 1 at (0,0) and an idle woodcutter 2 at (1,1) with no workers.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class SetPausedDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>
    /// Pausing the woodcutter stores <c>paused</c> true; staffing it then starts no cycle (it stays <c>Idle</c> in the stored row); resuming
    /// stores false and the same request starts its cycle.
    /// </summary>
    [Fact]
    public async Task Post_PausedThenResumed_IsStoredAndHoldsTheCycle()
    {
        (Guid player, Guid _) = await StoreCityAsync(Factory, "pauser", 1000, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0, logs: 0));

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(player, "set-paused", """{"buildingId":2,"paused":true}""")).Status);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(player, "assign-workers", """{"buildingId":2,"workers":1}""")).Status);
        Assert.Equal((true, "Idle"), await WoodcutterAsync());

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(player, "set-paused", """{"buildingId":2,"paused":false}""")).Status);
        Assert.Equal((false, "Producing"), await WoodcutterAsync());
    }

    /// <summary>Sends a command with a new request id over HTTP; <paramref name="fields"/> is the body's JSON without its request id.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="command">The command's path segment.</param>
    /// <param name="fields">The body's other fields, as a JSON object.</param>
    /// <returns>The answer.</returns>
    private Task<ApiResponse> PostAsync(Guid player, string command, string fields) =>
        ApiClient.PostAsync(Factory, "/api/v1/commands/" + command, player, $$"""{"requestId":"{{Guid.CreateVersion7()}}",""" + fields[1..]);

    /// <summary>The stored woodcutter row's <c>paused</c> and <c>phase</c>.</summary>
    /// <returns>The two columns.</returns>
    private async Task<(bool Paused, string Phase)> WoodcutterAsync()
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        var row = await db.CityBuildings.AsNoTracking().SingleAsync(building => building.BuildingId == 2, TestContext.Current.CancellationToken);
        return (row.Paused, row.Phase);
    }
}
