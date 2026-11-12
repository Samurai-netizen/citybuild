using CityBuilder.Application.Audit;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Golden;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.Audit;

/// <summary>
/// The ledger audit (M0-64) on PostgreSQL: players play a long seeded random game over HTTP (every command and settles, the server clock
/// moved by <see cref="PostgresApiFactory.Clock"/>), then <see cref="LedgerAuditor"/> audits every stored city and finds nothing; and a
/// ledger row planted by hand is found. The auditor's rules case by case: <c>LedgerAuditorTests</c> (Application tests).
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class LedgerAuditDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The players, each with its own seed; they take turns.</summary>
    private static readonly int[] _seeds = [11, 22, 33];

    /// <summary>
    /// The ledger reasons the game must write, so the audit covers each kind of row: every one the server writes but the two refunds (an
    /// upgrade has to land on a running cycle, which this game doesn't reach reliably; <c>UpgradeBuildingDatabaseTests</c> and
    /// <c>SettleCityTripTests</c> cover them).
    /// </summary>
    private static readonly string[] _reachedReasons =
    [
        "STARTING_GRANT", "COST_PLACE", "COST_UPGRADE", "COST_RESEARCH", "COST_ROAD", "SETTLEMENT_PRODUCED", "SETTLEMENT_CONSUMED",
        "SETTLEMENT_DELIVERED",
    ];

    /// <summary>How many turns each player plays.</summary>
    private const int _stepsPerPlayer = 300;

    /// <summary>
    /// After a long random game of three players, interleaved, with the clock moving 0 to 90 seconds before each step and now and then
    /// 1 to 6 hours, every city's ledger reconciles: starting grant first, an unbroken balance chain, and each item's sum equal to its city
    /// total. Every command succeeded at least once and the ledger holds each reason of <see cref="_reachedReasons"/>, so the game reached
    /// each write path.
    /// </summary>
    /// <returns>A task that completes when the audit is checked.</returns>
    [Fact]
    public async Task AuditAllAsync_AfterLongRandomGameOverHttp_FindsNoMismatch()
    {
        var clock = new Random(7);
        var players = new List<RandomHttpPlayer>();
        try
        {
            for (int index = 0; index < _seeds.Length; index++)
            {
                players.Add(await RandomHttpPlayer.StartAsync(Factory, $"audit-{index}", _seeds[index]));
            }

            for (int step = 0; step < _stepsPerPlayer; step++)
            {
                foreach (RandomHttpPlayer player in players)
                {
                    long seconds = clock.Next(40) == 0 ? 3600L * (1 + clock.Next(6)) : clock.Next(91);
                    Factory.Clock.Advance(TimeSpan.FromSeconds(seconds));
                    await player.StepAsync();
                }
            }

            LedgerAuditReport report = await AuditAllAsync();

            Assert.Equal(_seeds.Length, report.CitiesAudited);
            Assert.True(report.IsClean, string.Join(Environment.NewLine, report.Mismatches));
            await using (var db = Database.CreateContext())
            {
                string[] reasons = await db.LedgerEntries.Select(row => row.Reason).Distinct().ToArrayAsync(TestContext.Current.CancellationToken);
                Assert.Empty(_reachedReasons.Except(reasons));
            }

            foreach (string command in RandomHttpPlayer.Commands)
            {
                string refusals = string.Join(", ", players.SelectMany(player => player.Refused).Where(refusal => refusal.Key.Command == command)
                    .GroupBy(refusal => refusal.Key.Code).Select(group => $"{group.Key} {group.Sum(refusal => refusal.Value)}"));
                Assert.True(players.Sum(player => player.Succeeded[command]) > 0, $"{command} never succeeded (refused: {refusals}).");
            }
        }
        finally
        {
            players.ForEach(player => player.Dispose());
        }
    }

    /// <summary>
    /// A new city's ledger (its starting grant) reconciles; a row written behind the server's back (5 logs, as the server panel's goods
    /// script would write it, <c>DEV_ADJUST</c>) is reported as the logs' sum off their city total of 0.
    /// </summary>
    /// <returns>A task that completes when the audit is checked.</returns>
    [Fact]
    public async Task AuditCityAsync_RowPlantedByHand_ReportsTotalMismatch()
    {
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(Factory, "audit-planted");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid cityId;
        await using (var db = Database.CreateContext())
        {
            cityId = await db.Cities.Where(city => city.PlayerId == player.PlayerId).Select(city => city.Id).SingleAsync(cancellationToken);
        }

        Assert.True((await AuditCityAsync(cityId)).IsClean);

        await using (var db = Database.CreateContext())
        {
            db.LedgerEntries.Add(new LedgerEntryRow
            {
                CityId = cityId,
                ItemId = "logs",
                Delta = 5,
                BalanceAfter = 5,
                Reason = "DEV_ADJUST",
                RefType = "dev-panel",
                RefId = "add",
                CreatedAt = PostgresApiFactory.Start,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        LedgerAuditReport report = await AuditCityAsync(cityId);

        Assert.Equal(new LedgerMismatch(cityId, LedgerMismatchKind.TotalMismatch, new ItemId("logs"), null, 5, 0), Assert.Single(report.Mismatches));
    }

    /// <summary>Runs the server's <see cref="LedgerAuditor"/> over every city, in a scope of its own (its own context).</summary>
    /// <returns>The report.</returns>
    private async Task<LedgerAuditReport> AuditAllAsync()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LedgerAuditor>().AuditAllAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Runs the server's <see cref="LedgerAuditor"/> over one city, in a scope of its own.</summary>
    /// <param name="cityId">The city.</param>
    /// <returns>The report.</returns>
    private async Task<LedgerAuditReport> AuditCityAsync(Guid cityId)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LedgerAuditor>().AuditCityAsync(cityId, TestContext.Current.CancellationToken);
    }
}
