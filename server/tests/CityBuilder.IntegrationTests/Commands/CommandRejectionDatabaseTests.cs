using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.Rules;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// A command a business rule refuses, on PostgreSQL (<c>05</c>: rejections are not stored; decision 014: time passed is never undone): the
/// settlement up to now is committed, exactly as a settle alone would, and no receipt and no cost are stored, so the same request id runs
/// again on a retry. Each test stores a player with the frame saw city (<see cref="DatabaseCommands.StorePlayerAsync"/>) and places a house
/// on the tile of its house, which is <c>TILE_OCCUPIED</c>.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CommandRejectionDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The state version of a city as stored before each test: its 0, saved once.</summary>
    private const long _storedVersion = 1;

    /// <summary>The request id of the refused command.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000000c1");

    /// <summary>A house on the tile of the city's house (0, 0): refused with <c>TILE_OCCUPIED</c>.</summary>
    private static readonly PlaceTestCommand _onTheHouse = new(_requestId, "house", 0, 0);

    /// <summary>
    /// A rejection 10 minutes after the last settle commits the settlement: the city is saved once (version + 1) and ends exactly as its twin
    /// settled alone at the same time, with the same ledger (settlement rows only, no <c>COST_PLACE</c>); no receipt is stored.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RuleRejection_CommitsTheSettlementButStoresNoReceipt()
    {
        (Guid player, Guid city) = await StorePlayerAsync(Factory, "builder");
        (Guid twin, Guid twinCity) = await StorePlayerAsync(Factory, "twin");
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        CommandResult result = await ExecuteAsync(Factory, player, _onTheHouse);

        SettleCityResult settled = await SettleAsync(Factory, twin);
        Assert.Equal((CommandOutcome.Rejected, MutationErrorCode.TileOccupied), (result.Outcome, result.ErrorCode));
        Assert.Null(result.ResponseJson);
        Assert.True(settled.Saved);
        CityAggregate stored = await LoadAsync(Factory, player);
        Assert.Equal(_storedVersion + 1, stored.StateVersion);
        Assert.Equal(StateOf(settled.City), StateOf(stored));
        Assert.Equal(Totals(await LedgerAsync(Database, twinCity)), Totals(await LedgerAsync(Database, city)));
        Assert.DoesNotContain(await LedgerAsync(Database, city), row => row.Reason.StartsWith("COST_", StringComparison.Ordinal));
        Assert.Empty(await ReceiptsAsync(Database, player));
    }

    /// <summary>
    /// The same refused request again, 10 minutes later, is no replay: it runs again, settles the new 10 minutes (version + 1 again) and is
    /// refused again, still without a receipt. A new request id with a free tile then succeeds.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RejectedRequestAgain_RunsAgain()
    {
        (Guid player, _) = await StorePlayerAsync(Factory, "builder");
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(CommandOutcome.Rejected, (await ExecuteAsync(Factory, player, _onTheHouse)).Outcome);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        CommandResult retry = await ExecuteAsync(Factory, player, _onTheHouse);

        Assert.Equal((CommandOutcome.Rejected, MutationErrorCode.TileOccupied), (retry.Outcome, retry.ErrorCode));
        CityAggregate stored = await LoadAsync(Factory, player);
        Assert.Equal(_storedVersion + 2, stored.StateVersion);
        Assert.Equal(PostgresApiFactory.Start.AddMinutes(20).ToUnixTimeSeconds(), stored.LastSettledUnixSec);
        Assert.Empty(await ReceiptsAsync(Database, player));
        Assert.Equal(CommandOutcome.Succeeded, (await ExecuteAsync(Factory, player, PlaceHouse(Guid.NewGuid()))).Outcome);
    }
}
