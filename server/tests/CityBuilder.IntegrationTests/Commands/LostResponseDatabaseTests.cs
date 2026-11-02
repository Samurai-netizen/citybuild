using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CityBuilder.Api.Auth;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;
using static CityBuilder.IntegrationTests.Persistence.DatabaseLocks;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// A response that never reaches the client, on PostgreSQL over HTTP (M0-56; <c>05</c>, idempotency rules): the server applied the command, the
/// client saw nothing, polled the city and sent the same body again. The retry answers with the bytes of the lost response, and the command
/// counts once: one building or research or assignment, one set of ledger rows, one receipt. The same with the client hanging up before
/// the server answers. Each city is built by hand (<see cref="HandBuiltCities"/>): 1000 coins, 100 logs, 50 planks.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class LostResponseDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The placement endpoint.</summary>
    private const string _placePath = "/api/v1/commands/place-building";

    /// <summary>The request id every test sends.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000005a1");

    /// <summary>The body of the sawmill placement, the one that costs two items.</summary>
    private static string PlaceSawmill => $$"""{"requestId":"{{_requestId}}","buildingTypeId":"sawmill","x":3,"y":4}""";

    /// <summary>A placement whose response is lost: one sawmill, its two costs once, one receipt.</summary>
    [Fact]
    public async Task PlaceBuilding_ResponseLost_TheRetryGetsTheSameBytesAndOneBuilding()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "builder", 1000, 100, 50, House(1));

        await LoseTheResponseAndRetryAsync(player, city, _placePath, PlaceSawmill);

        await AssertOneSawmillAsync(city);
        Assert.Equal(
            [$"coins -150 850 COST_PLACE command {_requestId}", $"logs -30 70 COST_PLACE command {_requestId}"],
            await RowsOfTheCommandAsync(city));
    }

    /// <summary>A research start whose response is lost: the research runs from second 0, its cost once, one receipt.</summary>
    [Fact]
    public async Task StartResearch_ResponseLost_TheRetryGetsTheSameBytesAndOneCost()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));

        await LoseTheResponseAndRetryAsync(player, city, "/api/v1/commands/start-research", $$"""{"requestId":"{{_requestId}}","researchId":"sharp_axes"}""");

        await using CityBuilderDbContext db = Database.CreateContext();
        CityRow stored = await db.Cities.AsNoTracking().SingleAsync(row => row.Id == city, TestContext.Current.CancellationToken);
        Assert.Equal(("sharp_axes", (long?)0), (stored.ResearchId, stored.ResearchStartedAt));
        Assert.Equal([$"coins -250 750 COST_RESEARCH command {_requestId}"], await RowsOfTheCommandAsync(city));
    }

    /// <summary>A worker assignment whose response is lost: the two workers stay on the woodcutter, workers cost nothing, one receipt.</summary>
    [Fact]
    public async Task AssignWorkers_ResponseLost_TheRetryGetsTheSameBytesAndOneAssignment()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "assigner", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));

        await LoseTheResponseAndRetryAsync(player, city, "/api/v1/commands/assign-workers", $$"""{"requestId":"{{_requestId}}","buildingId":2,"workers":2}""");

        await using CityBuilderDbContext db = Database.CreateContext();
        CityBuildingRow woodcutter = await db.CityBuildings.AsNoTracking()
            .SingleAsync(row => row.CityId == city && row.BuildingId == 2, TestContext.Current.CancellationToken);
        Assert.Equal((short)2, woodcutter.AssignedWorkers);
        Assert.Empty(await RowsOfTheCommandAsync(city));
    }

    /// <summary>
    /// The client hangs up while its request waits for the city's lock (PostgreSQL shows it waiting), so no answer can reach it: the retry
    /// answers 200, and the city holds one sawmill and its two costs, whether the hung-up request ended before or after its commit.
    /// </summary>
    [Fact]
    public async Task PlaceBuilding_ClientHangsUpBeforeTheAnswer_TheRetryEndsWithOneBuilding()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "builder", 1000, 100, 50, House(1));
        var locked = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        await using WebApplicationFactory<Program> patient = WithLockTimeout(Factory, 30_000);
        Task holder = HoldAsync(patient, player, locked, release.Task);
        await locked.Task.WaitAsync(TestContext.Current.CancellationToken);

        using var hangUp = new CancellationTokenSource();
        using HttpClient client = patient.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, _placePath)
        {
            Content = new StringContent(PlaceSawmill, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", patient.Services.GetRequiredService<AccessTokens>().Issue(player).Token);
        Task<HttpResponseMessage> abandoned = client.SendAsync(request, hangUp.Token);
        await WaitUntilWaitingAsync(Database, 1);

        await hangUp.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
        release.SetResult();
        await holder;

        ApiResponse retry = await ApiClient.PostAsync(patient, _placePath, player, PlaceSawmill);

        Assert.Equal(HttpStatusCode.OK, retry.Status);
        Assert.Equal(retry.Body, Assert.Single(await ReceiptsAsync(Database, player)).ResponseJson);
        await AssertOneSawmillAsync(city);
        Assert.Equal(
            [$"coins -150 850 COST_PLACE command {_requestId}", $"logs -30 70 COST_PLACE command {_requestId}"],
            await RowsOfTheCommandAsync(city));
    }

    /// <summary>
    /// Plays the lost response: sends the command and throws away what the client would have read (the test keeps it only to compare), waits a
    /// minute and polls <c>GET /city</c> as the client does when it hears nothing, which settles the minute and moves the city past the command's
    /// version; a minute later it sends the same body. The retry must answer with the lost bytes and store nothing, so the settled city stays
    /// as the poll left it, and the only receipt holds those bytes.
    /// </summary>
    /// <param name="player">The acting player.</param>
    /// <param name="city">Their city.</param>
    /// <param name="path">The command's endpoint.</param>
    /// <param name="body">The body, sent twice.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private async Task LoseTheResponseAndRetryAsync(Guid player, Guid city, string path, string body)
    {
        ApiResponse lost = await ApiClient.PostAsync(Factory, path, player, body);
        Assert.Equal(HttpStatusCode.OK, lost.Status);

        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        ApiResponse poll = await ApiClient.GetAsync(Factory, "/api/v1/city", player);
        Assert.Equal(HttpStatusCode.OK, poll.Status);
        Assert.True((long)poll.Json()["stateVersion"]! > (long)lost.Json()["stateVersion"]!, "The poll settles a minute, so the city is past the lost response.");
        string stored = await StoredAsync(Factory, Database, player, city);

        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        ApiResponse retry = await ApiClient.PostAsync(Factory, path, player, body);

        Assert.Equal((HttpStatusCode.OK, lost.Body), (retry.Status, retry.Body));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
        Assert.Equal(lost.Body, Assert.Single(await ReceiptsAsync(Database, player)).ResponseJson);
    }

    /// <summary>Checks that the city holds exactly one sawmill.</summary>
    /// <param name="city">The city.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    private async Task AssertOneSawmillAsync(Guid city)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(1, await db.CityBuildings.AsNoTracking().CountAsync(row => row.CityId == city && row.TypeId == "sawmill", TestContext.Current.CancellationToken));
    }

    /// <summary>The ledger rows that name the request id as lines in ordinal order; production settled meanwhile names no request.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The lines (<see cref="HandBuiltCities.Line"/>).</returns>
    private async Task<IEnumerable<string>> RowsOfTheCommandAsync(Guid city) =>
        (await LedgerAsync(Database, city)).Where(row => row.RefId == _requestId.ToString()).Select(Line).Order(StringComparer.Ordinal);
}
