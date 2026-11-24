using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Audit;
using CityBuilder.Application.Cities;
using CityBuilder.IntegrationTests.Golden;
using CityBuilder.IntegrationTests.Persistence;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.DependencyInjection;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.EndToEnd;

/// <summary>
/// The M1-02 acceptance over HTTP: the scripted player of <see cref="CharterPlayer"/> plays the server's Era I content (the default
/// <c>Content:FilePath</c>) through the real API and PostgreSQL with a fake clock, from the first login to the Town Charter. Every act is
/// accepted, the stored city at the end is the engine's, the era's whole tree is complete, and the ledger auditor finds no mismatch. The
/// player logs in again before its token expires (12 hours).
/// </summary>
/// <param name="server">The run's PostgreSQL server; the test gets a fresh database on it.</param>
[Trait("Category", "Integration")]
public sealed class CharterHttpTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The player's display name; logging in again with it finds the same player.</summary>
    private const string _name = "charter";

    /// <summary>How long the player uses one login before it logs in again: well inside the token's 12 hours.</summary>
    private const long _loginSeconds = 6 * 3600;

    /// <summary>
    /// The player sends the script over HTTP: each act answers 200; at the end the stored city equals the engine's, the Town Charter and
    /// every other node of the era are complete, and the city's ledger reconciles.
    /// </summary>
    /// <returns>A task that completes when the run is checked.</returns>
    [Fact]
    public async Task Play_CharterScriptOverHttp_ReachesTheTownCharterWithTheEnginesCity()
    {
        GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(Factory, _name);
        try
        {
            long clock = 0;
            long loggedInAt = 0;
            foreach ((long time, LoopAct[] acts) in CharterPlayer.Script)
            {
                Factory.Clock.Advance(TimeSpan.FromSeconds(time - clock));
                clock = time;
                if (time - loggedInAt >= _loginSeconds)
                {
                    player.Dispose();
                    player = await GoldenHttpPlayer.LoginAsync(Factory, _name);
                    loggedInAt = time;
                }

                foreach (LoopAct act in acts)
                {
                    (string command, object payload) = EraOneLoop.Request(act, Guid.NewGuid());
                    (HttpStatusCode status, JsonNode? body) = await player.SendAsync(command, payload);
                    Assert.True(status == HttpStatusCode.OK, $"t={time} {act}: {(int)status} {body?.ToJsonString()}");
                }
            }

            Factory.Clock.Advance(TimeSpan.FromSeconds(CharterPlayer.End.Clock - clock));
            if (CharterPlayer.End.Clock - loggedInAt >= _loginSeconds)
            {
                player.Dispose();
                player = await GoldenHttpPlayer.LoginAsync(Factory, _name);
            }

            JsonNode last = await player.GetCityAsync();
            Assert.Contains(CharterPlayer.Charter.Value, last["city"]!["research"]!["completed"]!.AsArray().Select(node => (string)node!));

            CityAggregate stored = await LoadAsync(Factory, player.PlayerId);
            CityState engine = CharterPlayer.End;
            long second = PostgresApiFactory.Start.ToUnixTimeSeconds() + engine.Clock;
            var expected = new CityAggregate(
                stored.CityId, stored.PlayerId, engine, stored.ResearchCompletedAt, second, stored.StateVersion, CharterPlayer.Content.ContentVersion, second);
            Assert.Equal(SampleCities.Describe(expected), SampleCities.Describe(stored));
            Assert.Equal(CharterPlayer.Content.Research.Select(node => node.Id).Order(), stored.State.CompletedResearch.Order());

            await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
            LedgerAuditReport report = await scope.ServiceProvider.GetRequiredService<LedgerAuditor>().AuditAllAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, report.CitiesAudited);
            Assert.True(report.IsClean, string.Join(Environment.NewLine, report.Mismatches));
        }
        finally
        {
            player.Dispose();
        }
    }
}
