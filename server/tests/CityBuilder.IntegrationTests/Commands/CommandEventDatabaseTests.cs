using System.Net;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// The command's player event (CR-0009, <c>command.&lt;type&gt;</c>) end to end on PostgreSQL over HTTP, with the server's real content and a
/// starter city per player: an accepted and a refused command each leave exactly one row in the command's transaction, a refusal at an
/// unchanged instant leaves its row without saving the city, and a replay leaves none. The response is the same as without recording (the
/// contract tests pin it); the rows are only in <c>player_events</c>.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CommandEventDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The place-building endpoint.</summary>
    private const string _path = "/api/v1/commands/place-building";

    /// <summary>A house on tile (0, 0) with the request id.</summary>
    /// <param name="requestId">The request id.</param>
    /// <returns>The body.</returns>
    private static string PlaceHouse(Guid requestId) => $$"""{"requestId":"{{requestId}}","buildingTypeId":"house","x":0,"y":0}""";

    /// <summary>An accepted command leaves one row: its type, the building type, <c>accepted</c>, no value, the settled city clock.</summary>
    [Fact]
    public async Task Post_Accepted_StoresOneEvent()
    {
        Guid player = await StoreStarterAsync("accepted");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));

        ApiResponse response = await ApiClient.PostAsync(Factory, _path, player, PlaceHouse(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        PlayerEventRow row = Assert.Single(await PlayerEventsAsync(Database, player));
        Assert.Equal(
            ("command.place-building", "house", "accepted", (long?)null, (await LoadAsync(Factory, player)).State.Clock, Factory.Clock.GetUtcNow()),
            (row.EventType, row.Detail, row.Outcome, row.Value, row.CityClock, row.CreatedAt));
    }

    /// <summary>
    /// A command refused in the same second as the one before it (the tile is taken; nothing to settle) leaves one row with the error code,
    /// and the city is not saved: its state version stays.
    /// </summary>
    [Fact]
    public async Task Post_RefusedWithNothingToSettle_StoresOneEventAndKeepsTheVersion()
    {
        Guid player = await StoreStarterAsync("refused");
        Assert.Equal(HttpStatusCode.OK, (await ApiClient.PostAsync(Factory, _path, player, PlaceHouse(Guid.NewGuid()))).Status);
        long version = (await LoadAsync(Factory, player)).StateVersion;

        ApiResponse response = await ApiClient.PostAsync(Factory, _path, player, PlaceHouse(Guid.NewGuid()));

        Assert.Equal("TILE_OCCUPIED", (string)response.Json()["code"]!);
        Assert.Equal(version, (await LoadAsync(Factory, player)).StateVersion);
        Assert.Equal(
            ["accepted", "TILE_OCCUPIED"], (await PlayerEventsAsync(Database, player)).Select(row => row.Outcome).ToArray());
    }

    /// <summary>The same request again is a replay: the stored rows don't change.</summary>
    [Fact]
    public async Task Post_Replay_StoresNoEvent()
    {
        Guid player = await StoreStarterAsync("replay");
        string body = PlaceHouse(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, (await ApiClient.PostAsync(Factory, _path, player, body)).Status);
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));

        ApiResponse again = await ApiClient.PostAsync(Factory, _path, player, body);

        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Single(await PlayerEventsAsync(Database, player));
    }

    /// <summary>
    /// A <c>GET /city</c> an hour after the research <c>frame_saw</c> started settles it to completion: one <c>research.completed</c> row
    /// with the research id at the settled clock, in the settle's transaction.
    /// </summary>
    [Fact]
    public async Task GetCity_ResearchCompletedInTheWindow_StoresItsEvent()
    {
        (Guid player, Guid city) = (Guid.NewGuid(), Guid.NewGuid());
        await StoreAsync(
            Factory, new Player(player, "research"),
            WithFrameSawRunning(ContentOf(Factory), city, player, PostgresApiFactory.Start.ToUnixTimeSeconds()));
        Factory.Clock.Advance(TimeSpan.FromHours(1));

        ApiResponse response = await ApiClient.GetAsync(Factory, "/api/v1/city", player);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        PlayerEventRow row = Assert.Single(await PlayerEventsAsync(Database, player));
        Assert.Equal(
            ("research.completed", "frame_saw", (string?)null, (long?)null, (await LoadAsync(Factory, player)).State.Clock),
            (row.EventType, row.Detail, row.Outcome, row.Value, row.CityClock));
    }

    /// <summary>Stores a player with the starter city, last settled at the factory's start.</summary>
    /// <param name="name">The player's dev name.</param>
    /// <returns>The player.</returns>
    private async Task<Guid> StoreStarterAsync(string name)
    {
        (Guid player, Guid city) = (Guid.NewGuid(), Guid.NewGuid());
        await StoreAsync(Factory, new Player(player, name), Starter(ContentOf(Factory), city, player, PostgresApiFactory.Start.ToUnixTimeSeconds()));
        return player;
    }
}
