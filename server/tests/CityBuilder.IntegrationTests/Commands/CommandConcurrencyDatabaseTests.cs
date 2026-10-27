using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Storage;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;
using static CityBuilder.IntegrationTests.Persistence.DatabaseLocks;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// Identical commands at the same time on PostgreSQL (<c>05</c> and <c>06</c>, idempotency; ADR-0004): ten requests with one request id
/// queued behind the city's row lock apply the command once and all answer with the same response; and when the identical request's receipt
/// is committed while this one runs, PostgreSQL's unique key on <c>command_receipts</c> refuses the second receipt, everything this request
/// staged is rolled back, and the committed receipt answers. Each test stores players with the frame saw city
/// (<see cref="DatabaseCommands.StorePlayerAsync"/>) and places a house.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CommandConcurrencyDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>How many identical requests run at once.</summary>
    private const int _parallel = 10;

    /// <summary>The state version of a city as stored before each test: its 0, saved once.</summary>
    private const long _storedVersion = 1;

    /// <summary>
    /// The lock timeout of the server the requests run on: 30 s, so a request that waits for the held lock, or on the unique index, doesn't
    /// give up while the test waits for the others (the default 3 s could run out on a slow machine).
    /// </summary>
    private const int _lockTimeoutMilliseconds = 30_000;

    /// <summary>The request id every request of a test sends.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000000b1");

    /// <summary>
    /// Ten identical requests queued behind the city's held lock (PostgreSQL shows all ten waiting) while the clock moves on 10 minutes: the
    /// first applies the command, the nine after it find its receipt under the lock and replay it, so all ten answer with one text. The city
    /// is saved once (version + 1) with one receipt, and ends exactly as its twin that got the command once at the same time, with the same
    /// ledger: one mutation, one settle, one cost.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TenIdenticalRequestsAtOnce_ApplyOnceAndAllGetTheSameResponse()
    {
        (Guid player, Guid city) = await StorePlayerAsync(Factory, "racer");
        (Guid twin, Guid twinCity) = await StorePlayerAsync(Factory, "twin");
        await using WebApplicationFactory<Program> server = WithLockTimeout(Factory, _lockTimeoutMilliseconds);
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task holder = HoldAsync(server, player, locked, release.Task);
        await locked.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task<CommandResult>[] queued =
            [.. Enumerable.Range(0, _parallel).Select(_ => Task.Run(() => ExecuteAsync(server, player, PlaceHouse(_requestId))))];
        await WaitUntilWaitingAsync(Database, _parallel);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        release.SetResult();
        await holder;
        CommandResult[] results = await Task.WhenAll(queued);
        Assert.Equal(CommandOutcome.Succeeded, (await ExecuteAsync(server, twin, PlaceHouse(Guid.NewGuid()))).Outcome);

        Assert.Single(results, result => result.Outcome == CommandOutcome.Succeeded);
        Assert.Equal(_parallel - 1, results.Count(result => result.Outcome == CommandOutcome.Replayed));
        Assert.Single(results.Select(result => result.ResponseJson).Distinct(StringComparer.Ordinal));
        Assert.Equal(results[0].ResponseJson, Assert.Single(await ReceiptsAsync(Database, player)).ResponseJson);
        CityAggregate stored = await LoadAsync(server, player);
        Assert.Equal(_storedVersion + 1, stored.StateVersion);
        Assert.Equal(StateOf(await LoadAsync(server, twin)), StateOf(stored));
        Assert.Equal(Totals(await LedgerAsync(Database, twinCity)), Totals(await LedgerAsync(Database, city)));
    }

    /// <summary>
    /// An identical request's receipt (same payload) is committed while this request runs: this request didn't see it under the lock,
    /// settled and placed the house, then waited on the unique key with its receipt. PostgreSQL refuses it, the whole transaction rolls back
    /// (the settle of the 10 minutes, the house, the cost and the receipt), and the committed receipt's response answers, byte for byte.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_IdenticalReceiptCommittedWhileItRuns_RollsBackAndReturnsThatResponse()
    {
        const string winner = "{\"won\": \"by the other request\"}";

        byte[] hash = CommandPayload.Hash(PlaceHouse(_requestId));

        (CommandResult result, string before, string after) = await RaceAsync(hash, winner);

        Assert.Equal((CommandOutcome.Replayed, winner), (result.Outcome, result.ResponseJson));
        Assert.Equal(before + "\n" + ReceiptLine(_requestId, PlaceTestCommand.Type, hash, winner), after);
    }

    /// <summary>
    /// The receipt committed while this request runs has the same request id but another payload: the same rollback, then
    /// <c>IDEMPOTENCY_CONFLICT</c>, and the city keeps only the other request's receipt.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ReceiptOfAnotherPayloadCommittedWhileItRuns_RollsBackAndIsAConflict()
    {
        const string winner = "{\"other\": \"payload\"}";

        byte[] hash = CommandPayload.Hash(PlaceHouse(_requestId, x: FreeX + 1));

        (CommandResult result, string before, string after) = await RaceAsync(hash, winner);

        Assert.Equal(CommandOutcome.IdempotencyConflict, result.Outcome);
        Assert.Equal(before + "\n" + ReceiptLine(_requestId, PlaceTestCommand.Type, hash, winner), after);
    }

    /// <summary>
    /// The receipt race: stores a player, moves the clock on 10 minutes, inserts a receipt for <see cref="_requestId"/> in a transaction of
    /// the test's own without committing it, places the house with that request id, waits until PostgreSQL shows the request waiting (on the
    /// unique key), then commits the receipt and waits for the request.
    /// </summary>
    /// <param name="payloadHash">The payload hash of the receipt the test commits.</param>
    /// <param name="responseJson">Its response text.</param>
    /// <returns>How the request ended, and everything stored about the city before and after it (<see cref="DatabaseCommands.StoredAsync"/>).</returns>
    private async Task<(CommandResult Result, string Before, string After)> RaceAsync(byte[] payloadHash, string responseJson)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (Guid player, Guid city) = await StorePlayerAsync(Factory, "racer");
        await using WebApplicationFactory<Program> server = WithLockTimeout(Factory, _lockTimeoutMilliseconds);
        string before = await StoredAsync(server, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        await using CityBuilderDbContext db = Database.CreateContext();
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.CommandReceipts.Add(new CommandReceiptRow
        {
            PlayerId = player,
            RequestId = _requestId,
            CommandType = PlaceTestCommand.Type,
            PayloadHash = payloadHash,
            ResponseJson = responseJson,
            CreatedAt = Factory.Clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);

        Task<CommandResult> request = Task.Run(() => ExecuteAsync(server, player, PlaceHouse(_requestId)), cancellationToken);
        await WaitUntilWaitingAsync(Database, 1);
        await transaction.CommitAsync(cancellationToken);
        CommandResult result = await request;

        return (result, before, await StoredAsync(server, Database, player, city));
    }
}
