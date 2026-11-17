using CityBuilder.Application.Audit;
using CityBuilder.Application.Cities;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Concurrency;

/// <summary>
/// What every race of the concurrency suite (M0-75) must leave behind, whoever won: the ledger auditor (M0-64) finds no mismatch, no stock or
/// purse is below 0, no more workers are assigned or under way than the city provides, no two buildings share an id or a tile, and every
/// ledger row a command wrote is written once and belongs to a stored receipt.
/// </summary>
internal static class SoundCity
{
    /// <summary>Asserts that the stored city is sound, as the class says.</summary>
    /// <param name="server">The server on the test's database.</param>
    /// <param name="database">The test's database.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cityId">Their city.</param>
    /// <returns>A task that completes when every check passed.</returns>
    public static async Task AssertSoundAsync(WebApplicationFactory<Program> server, TestDatabase database, Guid playerId, Guid cityId)
    {
        await using (AsyncServiceScope scope = server.Services.CreateAsyncScope())
        {
            LedgerAuditReport report = await scope.ServiceProvider.GetRequiredService<LedgerAuditor>()
                .AuditCityAsync(cityId, TestContext.Current.CancellationToken);
            Assert.True(report.IsClean, string.Join(Environment.NewLine, report.Mismatches));
        }

        CityAggregate stored = await LoadAsync(server, playerId);
        CityState city = stored.State;
        GameContent content = ContentOf(server);
        Assert.True(city.Purse >= 0, $"purse {city.Purse}");
        Assert.All(city.Buildings, building => Assert.All(building.Store, stock => Assert.True(stock.Value >= 0, $"{building.Id} {stock.Key} {stock.Value}")));
        Assert.True(CityQueries.FreeWorkers(city, content) >= 0, $"free workers {CityQueries.FreeWorkers(city, content)}");
        Assert.Equal(city.Buildings.Count, city.Buildings.Select(building => building.Id).Distinct().Count());
        Assert.Equal(city.Buildings.Count, city.Buildings.Select(building => (building.X, building.Y)).Distinct().Count());

        List<LedgerEntryRow> commandRows = [.. (await LedgerAsync(database, cityId)).Where(row => row.RefType == "command")];
        Assert.All(
            commandRows.GroupBy(row => (row.RefId, row.ItemId, row.Reason)),
            rows => Assert.True(rows.Count() == 1, $"{rows.Key} written {rows.Count()} times"));
        HashSet<string> receipts = [.. (await ReceiptsAsync(database, playerId)).Select(receipt => receipt.RequestId.ToString())];
        Assert.All(commandRows, row => Assert.Contains(row.RefId!, receipts));
    }
}
