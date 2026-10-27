using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// Idempotency of <see cref="CommandPipeline"/> on PostgreSQL, one request after another (<c>05</c>, idempotency rules; <c>07</c>, replay
/// threats; ADR-0004): a retry with the same request id and payload returns the stored response text and writes nothing, also on a restarted
/// host; the same request id with another payload or on another command is a conflict that writes nothing; another player's request id
/// never matches. Each test stores players with the frame saw city (<see cref="DatabaseCommands.StorePlayerAsync"/>) and places a house.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CommandIdempotencyDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The request id most tests send.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000000a1");

    /// <summary>
    /// A retry of a successful command, 10 minutes later, returns the first response text byte for byte and writes nothing: no ledger row, no
    /// receipt, no new state version, and no settle of the 10 minutes. The first request stored one receipt and its <c>COST_PLACE</c> row,
    /// referenced by the request id.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SameRequestAgain_ReturnsTheIdenticalResponseAndWritesNothing()
    {
        (Guid player, Guid city) = await StorePlayerAsync(Factory, "builder");
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        CommandResult first = await ExecuteAsync(Factory, player, PlaceHouse(_requestId));
        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        CommandResult retry = await ExecuteAsync(Factory, player, PlaceHouse(_requestId));

        Assert.Equal(CommandOutcome.Succeeded, first.Outcome);
        Assert.Equal(CommandOutcome.Replayed, retry.Outcome);
        Assert.Equal(first.ResponseJson, retry.ResponseJson);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
        Assert.Equal(first.ResponseJson, Assert.Single(await ReceiptsAsync(Database, player)).ResponseJson);
        LedgerEntryRow cost = Assert.Single(await LedgerAsync(Database, city), row => row.Reason == "COST_PLACE");
        Assert.Equal(("command", _requestId.ToString()), (cost.RefType, cost.RefId));
    }

    /// <summary>
    /// A retry on a new host on the same database (the first host stopped: a server restart, nothing kept in memory) returns the stored
    /// response text and writes nothing.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SameRequestAfterAHostRestart_ReturnsTheStoredResponse()
    {
        (Guid player, Guid city) = await StorePlayerAsync(Factory, "builder");
        CommandResult first;
        await using (var before = new PostgresApiFactory(Database))
        {
            before.Clock.Advance(TimeSpan.FromMinutes(10));
            first = await ExecuteAsync(before, player, PlaceHouse(_requestId));
        }

        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromMinutes(20));

        CommandResult retry = await ExecuteAsync(Factory, player, PlaceHouse(_requestId));

        Assert.Equal(CommandOutcome.Succeeded, first.Outcome);
        Assert.Equal((CommandOutcome.Replayed, first.ResponseJson), (retry.Outcome, retry.ResponseJson));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>The same request id with another payload (another tile) is <c>IDEMPOTENCY_CONFLICT</c> and writes nothing.</summary>
    [Fact]
    public async Task ExecuteAsync_SameRequestIdOtherPayload_IsAConflictAndWritesNothing() =>
        await AssertConflictAsync(PlaceHouse(_requestId, x: FreeX + 1));

    /// <summary>The same request id on another command (an upgrade) is <c>IDEMPOTENCY_CONFLICT</c> and writes nothing.</summary>
    [Fact]
    public async Task ExecuteAsync_SameRequestIdOtherCommand_IsAConflictAndWritesNothing() =>
        await AssertConflictAsync(new UpgradeTestCommand(_requestId, HouseId));

    /// <summary>
    /// Another player sending the first player's request id, with the same payload or another, gets no replay and no conflict: the command
    /// runs as new on their own city, with a receipt of their own, and the first player's city, ledger and receipt stay as they were
    /// (<c>05</c>: the key includes the player).
    /// </summary>
    /// <param name="x">The other player's tile column: the first player's, or another.</param>
    [Theory]
    [InlineData(FreeX)]
    [InlineData(FreeX + 1)]
    public async Task Security_AnotherPlayersRequestId_NeverMatches(int x)
    {
        (Guid player, Guid city) = await StorePlayerAsync(Factory, "builder");
        (Guid other, Guid otherCity) = await StorePlayerAsync(Factory, "other");
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(CommandOutcome.Succeeded, (await ExecuteAsync(Factory, player, PlaceHouse(_requestId))).Outcome);
        string stored = await StoredAsync(Factory, Database, player, city);

        CommandResult result = await ExecuteAsync(Factory, other, PlaceHouse(_requestId, x));

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
        Assert.Equal(_requestId, Assert.Single(await ReceiptsAsync(Database, other)).RequestId);
        CityAggregate otherStored = await LoadAsync(Factory, other);
        Assert.Contains(otherStored.State.Buildings, building => (building.X, building.Y) == (x, FreeY));
        Assert.Single(await LedgerAsync(Database, otherCity), row => row.Reason == "COST_PLACE");
    }

    /// <summary>
    /// Places the house with <see cref="_requestId"/>, then sends <paramref name="retry"/> 10 minutes later: it must be a conflict that writes
    /// nothing, not even the settle of the 10 minutes.
    /// </summary>
    /// <param name="retry">A command with the same request id and another payload or command type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private async Task AssertConflictAsync(ICityCommand retry)
    {
        (Guid player, Guid city) = await StorePlayerAsync(Factory, "builder");
        Assert.Equal(CommandOutcome.Succeeded, (await ExecuteAsync(Factory, player, PlaceHouse(_requestId))).Outcome);
        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        CommandResult result = await ExecuteAsync(Factory, player, retry);

        Assert.Equal(CommandOutcome.IdempotencyConflict, result.Outcome);
        Assert.Null(result.ResponseJson);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }
}
