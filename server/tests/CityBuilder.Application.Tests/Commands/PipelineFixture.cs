using System.Globalization;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Simulation;
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
    /// A city with two sawmills at clock 180, restored as stored (goods reach a store only by walkers, CR-0008, so the running cycle is set up
    /// directly), with 50 coins and no goods anywhere: cottages 1 (0,0) and 4 (3,0) give 4 workers; woodcutter 2 (1,0) is producing (its cycle
    /// from 180 to 240); sawmill 3 (2,0) is producing (its cycle from 180 to 300 took 2 logs); sawmill 5 (0,1) is idle with a worker and an
    /// empty store. Upgrading sawmill 3 gives its 2 logs back into its own store.
    /// </summary>
    public static InMemoryCityRepository TwoSawmillsRepository()
    {
        var state = new CityState(
            3 * StartClock,
            1,
            6,
            50,
            [
                new Building(1, new BuildingTypeId("cottage"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 1, BuildingPhase.Producing, 180, 240, 0, 60, 60, 180),
                new Building(3, new BuildingTypeId("sawmill"), 2, 0, 1, 1, BuildingPhase.Producing, 180, 300, 0, 120, 120, 180),
                new Building(4, new BuildingTypeId("cottage"), 3, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(5, new BuildingTypeId("sawmill"), 0, 1, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            ],
            [],
            activeResearch: null);
        return RepositoryWith(state);
    }
}
