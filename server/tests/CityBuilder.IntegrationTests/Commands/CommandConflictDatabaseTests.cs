using System.Net;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.AspNetCore.Mvc.Testing;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Contracts.ProblemAssert;
using static CityBuilder.IntegrationTests.Persistence.DatabaseLocks;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// A real lock timeout over HTTP on PostgreSQL (<c>05</c>, <c>CONCURRENCY_CONFLICT</c>; <c>06</c>, write transaction pattern): another request
/// holds the city's row lock longer than <c>Database:LockTimeoutMilliseconds</c>, so the request gives up with 409 and nothing is stored;
/// after the lock is released the same request, with the same request id, succeeds. A read gets the same 409 without a request id.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CommandConflictDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The lock timeout of the server the requests run on: short, so the test doesn't wait for the default.</summary>
    private const int _lockTimeoutMilliseconds = 200;

    /// <summary>The request id the tests send.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000003c1");

    /// <summary>
    /// A command that waits longer than the lock timeout answers 409 <c>CONCURRENCY_CONFLICT</c> with its request id and stores nothing: the
    /// city, its ledger and its receipts are as they were. Once the lock is free, the retry with the same request id places the house.
    /// </summary>
    [Fact]
    public async Task Post_CityLockedLongerThanTheTimeout_IsConflictAndTheRetrySucceeds()
    {
        (Guid player, Guid city) = await StorePlayerAsync(Factory, "locked");
        await using WebApplicationFactory<Program> timeoutServer = WithLockTimeout(Factory, _lockTimeoutMilliseconds);
        string before = await StoredAsync(Factory, Database, player, city);
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task holder = HoldAsync(timeoutServer, player, locked, release.Task);
        await locked.Task.WaitAsync(TestContext.Current.CancellationToken);

        ApiResponse conflict = await ApiClient.PostAsync(timeoutServer, "/api/v1/commands/place-building", player, PlaceBody());
        ApiResponse read = await ApiClient.GetAsync(timeoutServer, "/api/v1/city", player);
        release.SetResult();
        await holder;

        AssertProblem(conflict, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", _requestId.ToString());
        AssertProblem(read, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", requestId: null);
        Assert.Equal(before, await StoredAsync(Factory, Database, player, city));

        ApiResponse retry = await ApiClient.PostAsync(timeoutServer, "/api/v1/commands/place-building", player, PlaceBody());

        Assert.Equal(HttpStatusCode.OK, retry.Status);
        Assert.Equal(_requestId.ToString(), (string)retry.Json()["requestId"]!);
        Assert.Single(await ReceiptsAsync(Database, player));
    }

    /// <summary>A place-house body for the free tile of the stored city.</summary>
    /// <returns>The JSON text.</returns>
    private static string PlaceBody() =>
        $$"""{"requestId":"{{_requestId}}","buildingTypeId":"house","x":{{FreeX}},"y":{{FreeY}}}""";
}
