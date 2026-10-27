using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Players;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.Extensions.DependencyInjection;
using static CityBuilder.IntegrationTests.Persistence.SampleCities;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The receipt lookup (<see cref="ICommandReceiptRepository"/>, <c>05</c> idempotency rules, <c>06</c> <c>command_receipts</c>) on PostgreSQL,
/// through the server's registrations: a receipt saved with its city is found with its response text exactly as written (the column is
/// <c>json</c>, decision 041), and neither another request id nor another player's key finds it. Each test stores the sample city
/// (<see cref="SampleCities.Full"/>) and one receipt for it.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CommandReceiptRepositoryDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>
    /// A response with spacing and key order that <c>jsonb</c> would rewrite (to <c>{"a": [2, 3], "b": 1}</c>), so the test sees whether
    /// the text survives.
    /// </summary>
    private const string _response = "{\"b\": 1,\"a\":[2, 3]}";

    /// <summary>The request id of the stored receipt.</summary>
    private static readonly Guid _requestId = new("0b7e9c4a-0000-4000-8000-0000000000b1");

    /// <summary>The stored receipt's payload hash.</summary>
    private static readonly byte[] _hash = Enumerable.Range(0, CommandReceipt.PayloadHashLength).Select(value => (byte)value).ToArray();

    /// <summary>The receipt is found by its player and request id, with its command type, hash and response text byte for byte.</summary>
    [Fact]
    public async Task FindAsync_StoredReceipt_ReturnsItAsWritten()
    {
        await StoreCityWithReceiptAsync();

        CommandReceipt receipt = Assert.IsType<CommandReceipt>(await FindAsync(PlayerId, _requestId));

        Assert.Equal((_requestId, "place-building", _response), (receipt.RequestId, receipt.CommandType, receipt.ResponseJson));
        Assert.Equal(_hash, receipt.PayloadHash.ToArray());
    }

    /// <summary>Another request id of the same player finds nothing.</summary>
    [Fact]
    public async Task FindAsync_AnotherRequestId_ReturnsNull()
    {
        await StoreCityWithReceiptAsync();

        Assert.Null(await FindAsync(PlayerId, Guid.NewGuid()));
    }

    /// <summary>Another player with the same request id finds nothing: the key includes the player (<c>07</c>, replay threats).</summary>
    [Fact]
    public async Task Security_AnotherPlayersRequestId_FindsNoReceipt()
    {
        await StoreCityWithReceiptAsync();
        var otherPlayerId = new Guid("91a7e400-0000-4000-8000-000000000002");
        CityAggregate empty = Empty();
        await DatabaseCities.StoreAsync(
            Factory,
            new Player(otherPlayerId, "dev2"),
            new CityAggregate(Guid.NewGuid(), otherPlayerId, empty.State, [], empty.LastSettledUnixSec, 0, empty.ContentVersion));

        Assert.Null(await FindAsync(otherPlayerId, _requestId));
    }

    /// <summary>Stores the sample city's player and city, then saves the city once more in its transaction with the receipt.</summary>
    /// <returns>A task that completes when the receipt is committed.</returns>
    private async Task StoreCityWithReceiptAsync()
    {
        await DatabaseCities.StoreAsync(Factory, new Player(PlayerId, "dev1"), Full());
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICityTransactionRunner>().RunAsync(
            PlayerId,
            async (transaction, ct) =>
            {
                await transaction.SaveAsync([], new CommandReceipt(_requestId, "place-building", _hash, _response), ct);
                return 0;
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Looks up a receipt in a request scope of its own, outside any transaction.</summary>
    /// <param name="playerId">The player.</param>
    /// <param name="requestId">The request id.</param>
    /// <returns>The receipt, or <see langword="null"/>.</returns>
    private async Task<CommandReceipt?> FindAsync(Guid playerId, Guid requestId)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICommandReceiptRepository>()
            .FindAsync(playerId, requestId, TestContext.Current.CancellationToken);
    }
}
