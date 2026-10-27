using System.Globalization;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// The command pipeline over the in-memory fakes and <see cref="SettleFixture"/>'s content and cities, with a response writer that records
/// <c>respond</c> in the repository's events and writes a short JSON of what it was given.
/// </summary>
internal static class PipelineFixture
{
    /// <summary>The request id most tests send.</summary>
    public static readonly Guid RequestId = new("0b7e9c4a-0000-4000-8000-00000000000a");

    /// <summary>The player of the second city in <see cref="WithSecondPlayer"/>.</summary>
    public static readonly Guid OtherPlayerId = new("6f1d2c3b-0000-4000-8000-000000000002");

    /// <summary>A free tile of every fixture city: the cottage, the woodcutter and the manor or sawmill stand on row 0.</summary>
    public static PlaceTestCommand PlaceCottage(Guid? requestId = null) => new(requestId ?? RequestId, "cottage", 1, 1);

    /// <summary>
    /// The pipeline over the repository (also its unit of work and receipts), the fixture's content and the clock. Each call is a request of
    /// its own, with a new <see cref="CityTransactionRunner"/>, so a test can send commands again and again.
    /// </summary>
    public static CommandPipeline Pipeline(InMemoryCityRepository repository, TimeProvider clock) =>
        new(new RunnerPerRequest(repository), repository, new FixedContent(SettleFixture.Content), clock);

    /// <summary>
    /// A response writer that records <c>respond</c> and writes the request id, the new state version, the city clock, the server second
    /// and the number of buildings as JSON, so a test sees what it was given.
    /// </summary>
    public static Func<CommandSuccess, string> Writer(InMemoryCityRepository repository) => success =>
    {
        repository.Events.Add("respond");
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"requestId\":\"{success.RequestId}\",\"stateVersion\":{success.StateVersion},\"clock\":{success.City.State.Clock}," +
            $"\"serverTime\":{success.ServerTime.ToUnixTimeSeconds()},\"buildings\":{success.City.State.Buildings.Count}}}");
    };

    /// <summary>Adds a second player's city (the starter city, settled at <see cref="LastSettled"/>) to the repository.</summary>
    public static InMemoryCityRepository WithSecondPlayer(InMemoryCityRepository repository)
    {
        repository.Add(new CityAggregate(
            new Guid("6f1d2c3b-0000-4000-8000-0000000000c2"), OtherPlayerId, CityFactory.CreateStarterCity(SettleFixture.Content), researchCompletedAt: [],
            LastSettled.ToUnixTimeSeconds(), stateVersion: 0, SettleFixture.Content.ContentVersion));
        return repository;
    }

    /// <summary>
    /// A city with two sawmills, settled at clock 180 with no logs: cottages A (0,0) and B (3,0) give 4 workers; the woodcutter (1,0) makes a
    /// log every 60 s; sawmill 3 (2,0) took the 2 logs of clock 180 and is producing; sawmill 5 (0,1) completed at 180, has a worker and waits
    /// for logs. Upgrading sawmill 3 gives its 2 logs back, and the closing advance starts sawmill 5 with them.
    /// </summary>
    public static InMemoryCityRepository TwoSawmillsRepository()
    {
        CityState state = CityFactory.CreateStarterCity(SettleFixture.Content);
        Apply(state, CityRules.PlaceBuilding(state, SettleFixture.Content, new BuildingTypeId("cottage"), 0, 0));
        Apply(state, CityRules.PlaceBuilding(state, SettleFixture.Content, new BuildingTypeId("woodcutter"), 1, 0));
        Simulator.Advance(state, SettleFixture.Content, StartClock);
        Apply(state, CityRules.PlaceBuilding(state, SettleFixture.Content, new BuildingTypeId("sawmill"), 2, 0));
        Apply(state, CityRules.PlaceBuilding(state, SettleFixture.Content, new BuildingTypeId("cottage"), 3, 0));
        Simulator.Advance(state, SettleFixture.Content, StartClock);
        Apply(state, CityRules.PlaceBuilding(state, SettleFixture.Content, new BuildingTypeId("sawmill"), 0, 1));
        Simulator.Advance(state, SettleFixture.Content, StartClock);
        Simulator.Advance(state, SettleFixture.Content, 0);
        return RepositoryWith(state);
    }

    /// <summary>A mutation followed by the zero-length advance every command gets; a rejected mutation fails the test.</summary>
    private static void Apply(CityState state, MutationResult result)
    {
        Assert.True(result.IsOk, result.ToString());
        Simulator.Advance(state, SettleFixture.Content, 0);
    }
}
