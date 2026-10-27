using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Application.Persistence;
using CityBuilder.Application.Players;
using CityBuilder.Domain.Players;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// Cities on a test's database, through a server's own registrations: storing a player with a new city, loading a city as a request would,
/// settling it with <see cref="SettleCity"/>, and two cities on the server's content (<c>server/content/mvp-content.json</c>) to settle.
/// </summary>
internal static class DatabaseCities
{
    /// <summary>The research done in <see cref="WithFrameSawRunning"/> before clock 0.</summary>
    public static readonly ResearchId SharpAxes = new("sharp_axes");

    /// <summary>The 1-hour research <see cref="WithFrameSawRunning"/> starts at clock 0.</summary>
    public static readonly ResearchId FrameSaw = new("frame_saw");

    /// <summary>Stores a player and their new city (an insert: no lock to take), in a scope of its own.</summary>
    /// <param name="factory">The server.</param>
    /// <param name="player">The player.</param>
    /// <param name="city">The player's new city; stored at its state version + 1.</param>
    /// <returns>A task that completes when both are written.</returns>
    public static async Task StoreAsync(WebApplicationFactory<Program> factory, Player player, CityAggregate city)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IPlayerRepository>().Add(player);
        scope.ServiceProvider.GetRequiredService<ICityRepository>().Save(city, [], receipt: null);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Loads the player's stored city as a request would (in its transaction, which commits without a save).</summary>
    /// <param name="factory">The server.</param>
    /// <param name="playerId">The player.</param>
    /// <returns>The stored city.</returns>
    public static async Task<CityAggregate> LoadAsync(WebApplicationFactory<Program> factory, Guid playerId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICityTransactionRunner>().RunAsync(
            playerId, (transaction, _) => Task.FromResult(transaction.City), TestContext.Current.CancellationToken);
    }

    /// <summary>Settles the player's city to the server's now, as one request.</summary>
    /// <param name="factory">The server.</param>
    /// <param name="playerId">The player.</param>
    /// <returns>What the settle did.</returns>
    public static async Task<SettleCityResult> SettleAsync(WebApplicationFactory<Program> factory, Guid playerId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SettleCity>().ExecuteAsync(playerId, TestContext.Current.CancellationToken);
    }

    /// <summary>The content the server runs on.</summary>
    /// <param name="factory">The server.</param>
    /// <returns>Its validated content.</returns>
    public static GameContent ContentOf(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IContentProvider>().Content;

    /// <summary>A new player's city as the content's starting city describes it, last settled at <paramref name="lastSettledUnixSec"/>.</summary>
    /// <param name="content">The server's content.</param>
    /// <param name="cityId">The city's id.</param>
    /// <param name="playerId">The owner.</param>
    /// <param name="lastSettledUnixSec">The server second it was created and settled at.</param>
    /// <returns>The city, at state version 0.</returns>
    public static CityAggregate Starter(GameContent content, Guid cityId, Guid playerId, long lastSettledUnixSec) =>
        new(cityId, playerId, CityFactory.CreateStarterCity(content), [], lastSettledUnixSec, 0, content.ContentVersion);

    /// <summary>
    /// A working city at clock 0 on the server's content: a cottage (4 workers) with its workers on a woodcutter (2) and a sawmill (2), all
    /// completed; <c>sharp_axes</c> done (−200 ‰ woodcutter labor); 1000 coins, 100 logs, 50 planks. The player starts the 1-hour research
    /// <c>frame_saw</c> (400 coins, 30 planks; −200 ‰ sawmill labor) and the zero-length advance starts both producers, as a command would.
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <param name="cityId">The city's id.</param>
    /// <param name="playerId">The owner.</param>
    /// <param name="lastSettledUnixSec">The server second it was last settled at.</param>
    /// <returns>The city, at state version 0, canonical.</returns>
    public static CityAggregate WithFrameSawRunning(GameContent content, Guid cityId, Guid playerId, long lastSettledUnixSec)
    {
        Building[] buildings =
        [
            new(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(3, new BuildingTypeId("sawmill"), 2, 0, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
        ];
        var state = new CityState(
            0,
            era: 1,
            nextBuildingId: 4,
            [new(new ItemId("coins"), 1000), new(new ItemId("logs"), 100), new(new ItemId("planks"), 50)],
            buildings,
            [SharpAxes],
            activeResearch: null);
        MutationResult started = CityRules.StartResearch(state, content, FrameSaw);
        Assert.True(started.IsOk, started.ToString());
        Simulator.Advance(state, content, 0);
        return new CityAggregate(cityId, playerId, state, [new(SharpAxes, 0)], lastSettledUnixSec, 0, content.ContentVersion);
    }
}
