using System.Globalization;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// Commands on a test's database, through a server's own registrations but without HTTP (the pipeline's own tests; the endpoints have theirs):
/// a player with the frame saw city (<see cref="Persistence.DatabaseCities.WithFrameSawRunning"/>) to send them to, <see cref="CommandPipeline"/>
/// run as one request, a response writer in place of the endpoint's snapshot (a short JSON of the success), and the stored receipts and the
/// city as text, so a test can show that a request wrote nothing.
/// </summary>
internal static class DatabaseCommands
{
    /// <summary>A free tile of the frame saw city (column 4, row 5): its house, woodcutter, sawmill and scriptorium stand on row 4.</summary>
    public const int FreeX = 4;

    /// <summary>The row of <see cref="FreeX"/>.</summary>
    public const int FreeY = 5;

    /// <summary>The house of the frame saw city, on tile (4, 4): placing on it is <c>TILE_OCCUPIED</c>.</summary>
    public const int HouseId = 1;

    /// <summary>A new player's frame saw city places a house here, the cheapest building of the server's content.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="x">The tile's column; <see cref="FreeX"/> unless a test wants another payload.</param>
    /// <returns>The command.</returns>
    public static PlaceTestCommand PlaceHouse(Guid requestId, int x = FreeX) => new(requestId, "house", x, FreeY);

    /// <summary>Stores a new player with the frame saw city, last settled at <see cref="PostgresApiFactory.Start"/>, where the clock starts.</summary>
    /// <param name="factory">The server.</param>
    /// <param name="name">The player's dev name.</param>
    /// <returns>The player and their city.</returns>
    public static async Task<(Guid Player, Guid City)> StorePlayerAsync(WebApplicationFactory<Program> factory, string name)
    {
        (Guid player, Guid city) = (Guid.NewGuid(), Guid.NewGuid());
        await StoreAsync(
            factory,
            new Player(player, name),
            WithFrameSawRunning(ContentOf(factory), city, player, PostgresApiFactory.Start.ToUnixTimeSeconds()));
        return (player, city);
    }

    /// <summary>Runs the player's command through the server's <see cref="CommandPipeline"/> as one request: a scope of its own.</summary>
    /// <param name="factory">The server.</param>
    /// <param name="playerId">The acting player.</param>
    /// <param name="command">The command.</param>
    /// <returns>How the pipeline ended it.</returns>
    public static async Task<CommandResult> ExecuteAsync(WebApplicationFactory<Program> factory, Guid playerId, ICityCommand command)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CommandPipeline>()
            .ExecuteAsync(playerId, command, Respond, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The response writer: the request id, the new state version, the server second, the city clock and the number of buildings as JSON,
    /// so two responses are the same text only if they describe the same applied command.
    /// </summary>
    /// <param name="success">The applied command.</param>
    /// <returns>The response JSON.</returns>
    public static string Respond(CommandSuccess success) => string.Create(
        CultureInfo.InvariantCulture,
        $"{{\"requestId\":\"{success.RequestId}\",\"stateVersion\":{success.StateVersion}," +
        $"\"serverTime\":{success.ServerTime.ToUnixTimeSeconds()},\"clock\":{success.City.State.Clock}," +
        $"\"buildings\":{success.City.State.Buildings.Count}}}");

    /// <summary>The player's stored player events (CR-0009), in the order they were written.</summary>
    /// <param name="database">The test's database.</param>
    /// <param name="playerId">The player.</param>
    /// <returns>The rows.</returns>
    public static async Task<List<PlayerEventRow>> PlayerEventsAsync(TestDatabase database, Guid playerId)
    {
        await using CityBuilderDbContext db = database.CreateContext();
        return await db.PlayerEvents.AsNoTracking().Where(row => row.PlayerId == playerId).OrderBy(row => row.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The stored receipts of a player, by request id, read by a context of the test's own.</summary>
    /// <param name="database">The test's database.</param>
    /// <param name="playerId">The player.</param>
    /// <returns>Their <c>command_receipts</c> rows.</returns>
    public static async Task<List<CommandReceiptRow>> ReceiptsAsync(TestDatabase database, Guid playerId)
    {
        await using CityBuilderDbContext db = database.CreateContext();
        return await db.CommandReceipts.AsNoTracking().Where(row => row.PlayerId == playerId).OrderBy(row => row.RequestId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Everything stored about a player's city as text: its state version and settled state, every ledger row (with its running balance and
    /// reference) and every receipt, so one assertion shows that a request wrote nothing.
    /// </summary>
    /// <param name="factory">The server.</param>
    /// <param name="database">The test's database.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cityId">Their city.</param>
    /// <returns>The text.</returns>
    public static async Task<string> StoredAsync(WebApplicationFactory<Program> factory, TestDatabase database, Guid playerId, Guid cityId)
    {
        CityAggregate city = await LoadAsync(factory, playerId);
        IEnumerable<string> ledger = (await LedgerAsync(database, cityId)).Select(row =>
            string.Create(CultureInfo.InvariantCulture, $"{row.Id} {row.ItemId} {row.Delta} {row.BalanceAfter} {row.Reason} {row.RefType} {row.RefId}"));
        IEnumerable<string> events = (await PlayerEventsAsync(database, playerId)).Select(row =>
            string.Create(CultureInfo.InvariantCulture, $"event {row.EventType} {row.Detail} {row.Outcome} {row.Value} {row.CityClock}"));
        IEnumerable<string> receipts = (await ReceiptsAsync(database, playerId))
            .Select(row => ReceiptLine(row.RequestId, row.CommandType, row.PayloadHash, row.ResponseJson));
        return string.Join("\n", [$"version {city.StateVersion}", StateOf(city), .. ledger, .. receipts, .. events]);
    }

    /// <summary>
    /// Asserts what a rule's refusal at an unchanged instant stores (CR-0009): the city, its ledger and its receipts exactly as
    /// <paramref name="before"/>, and after them one player event per refusal, in order, each a <c>command.&lt;type&gt;</c> row with that error
    /// code as its outcome and no value. Nothing else.
    /// </summary>
    /// <param name="factory">The server.</param>
    /// <param name="database">The test's database.</param>
    /// <param name="playerId">The refused player.</param>
    /// <param name="cityId">Their city.</param>
    /// <param name="before">What <see cref="StoredAsync"/> gave before the refused requests.</param>
    /// <param name="codes">The error code of each refusal, in the order they were sent.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    public static async Task AssertStoredWithRefusalsAsync(
        WebApplicationFactory<Program> factory, TestDatabase database, Guid playerId, Guid cityId, string before, params string[] codes)
    {
        string after = await StoredAsync(factory, database, playerId, cityId);

        Assert.StartsWith(before + "\n", after, StringComparison.Ordinal);
        string[] added = after[(before.Length + 1)..].Split('\n');
        Assert.Equal(codes.Length, added.Length);
        for (int i = 0; i < codes.Length; i++)
        {
            Assert.Matches($"^event command\\.[a-z-]+ [a-z0-9_]* {codes[i]}  [0-9]+$", added[i]);
        }
    }

    /// <summary>The line <see cref="StoredAsync"/> writes for one receipt, so a test can name the receipt it expects there.</summary>
    /// <param name="requestId">The receipt's request id.</param>
    /// <param name="commandType">Its command type.</param>
    /// <param name="payloadHash">Its payload hash.</param>
    /// <param name="responseJson">Its response text.</param>
    /// <returns>The line.</returns>
    public static string ReceiptLine(Guid requestId, string commandType, byte[] payloadHash, string responseJson) =>
        $"{requestId} {commandType} {Convert.ToHexString(payloadHash)} {responseJson}";
}
