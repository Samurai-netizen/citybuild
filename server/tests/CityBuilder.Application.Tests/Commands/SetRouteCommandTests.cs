using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.SetRoute;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// <see cref="SetRouteCommand"/> (CR-0008-T1 part G): its shape check, its pinned canonical payload, its ledger reasons (none: goods only
/// move), that it applies the engine's rule (<see cref="CourierRules.SetRoute"/>) and nothing else, and one run through
/// <see cref="CommandPipeline"/> over the in-memory fakes. The rule's checks are the engine's tests; the endpoint and PostgreSQL are the
/// integration tests'. The test city is the two-sawmill city of <see cref="PipelineFixture.TwoSawmillsRepository"/> with courier station 6
/// at (1,1), its one worker courier 6/1 at the station.
/// </summary>
public sealed class SetRouteCommandTests
{
    /// <summary>The request id of the tests.</summary>
    private static readonly Guid _requestId = new("0199a1b2-c3d4-7e5f-8a9b-00000000000a");

    /// <summary>Station id and slot below 1 are a wrong shape: no city has them (OD-13).</summary>
    /// <param name="stationId">The station id.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData(6, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(6, 0, false)]
    [InlineData(-1, 1, false)]
    public void ValidateShape_StationAndSlot_RefusesBelowOne(int stationId, int slot, bool valid) =>
        Assert.Equal(valid, new SetRouteCommand(_requestId, stationId, slot, Route()).ValidateShape() is null);

    /// <summary>
    /// 0 to 16 stops are a shape (0 clears the route; 1 or more than the content's limit is the rule's <c>INVALID_ROUTE</c>); a missing list
    /// or more than 16 stops is not.
    /// </summary>
    /// <param name="stops">How many stops.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void ValidateShape_StopCount_AcceptsZeroToSixteen(int stops, bool valid) =>
        Assert.Equal(valid, new SetRouteCommand(_requestId, 6, 1, [.. Enumerable.Range(0, stops).Select(stop => Take(2 + (stop % 2)))]).ValidateShape() is null);

    /// <summary>A missing stop list, a null stop, a building id below 1, or a stop without actions is a wrong shape.</summary>
    [Fact]
    public void ValidateShape_BrokenStops_AreRefused()
    {
        CommandRouteAction take = new("take", "logs", "all");

        Assert.NotNull(new SetRouteCommand(_requestId, 6, 1, null).ValidateShape());
        Assert.NotNull(new SetRouteCommand(_requestId, 6, 1, [Take(2), null]).ValidateShape());
        Assert.NotNull(new SetRouteCommand(_requestId, 6, 1, [Take(2), new CommandRouteStop(0, [take])]).ValidateShape());
        Assert.NotNull(new SetRouteCommand(_requestId, 6, 1, [Take(2), new CommandRouteStop(5, null)]).ValidateShape());
        Assert.NotNull(new SetRouteCommand(_requestId, 6, 1, [Take(2), new CommandRouteStop(5, [])]).ValidateShape());
        Assert.NotNull(new SetRouteCommand(_requestId, 6, 1, [Take(2), new CommandRouteStop(5, [take, null])]).ValidateShape());
    }

    /// <summary>1 to 8 actions per stop are a shape (more than the content's limit is the rule's); 9 are not.</summary>
    /// <param name="actions">How many actions the second stop has.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData(1, true)]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void ValidateShape_ActionCount_AcceptsOneToEight(int actions, bool valid) =>
        Assert.Equal(
            valid,
            new SetRouteCommand(_requestId, 6, 1, [Take(2), new CommandRouteStop(5, [.. Enumerable.Repeat(new CommandRouteAction("put", "logs", "all"), actions)])])
                .ValidateShape() is null);

    /// <summary>
    /// An action's kind is <c>take</c> or <c>put</c>, its item id of the content-id form, its amount one of the three forms; anything else is
    /// a wrong shape. An unknown item or preset of the right form is left to the rule.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="itemId">The item id.</param>
    /// <param name="amount">The amount's text.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData("put", "logs", "all", true)]
    [InlineData("take", "no_such_item", "no_such_preset", true)]
    [InlineData("put", "logs", "50%", true)]
    [InlineData("put", "logs", "12", true)]
    [InlineData("Put", "logs", "all", false)]
    [InlineData("drop", "logs", "all", false)]
    [InlineData("", "logs", "all", false)]
    [InlineData("put", "Logs", "all", false)]
    [InlineData("put", "", "all", false)]
    [InlineData("put", "logs", "0%", false)]
    [InlineData("put", "logs", "1.5", false)]
    [InlineData("put", "logs", "", false)]
    public void ValidateShape_Action_AcceptsTheContractsFormsOnly(string kind, string itemId, string amount, bool valid) =>
        Assert.Equal(
            valid,
            new SetRouteCommand(_requestId, 6, 1, [Take(2), new CommandRouteStop(5, [new CommandRouteAction(kind, itemId, amount)])]).ValidateShape() is null);

    /// <summary>The message names the field and the stop, so the client knows what to fix.</summary>
    [Fact]
    public void ValidateShape_NamesTheFieldAndTheStop() =>
        Assert.Equal(
            "kind must be \"take\" or \"put\" (stop 2).",
            new SetRouteCommand(_requestId, 6, 1, [Take(2), new CommandRouteStop(5, [new CommandRouteAction("drop", "logs", "all")])]).ValidateShape());

    /// <summary>
    /// The canonical payload is <c>set-route</c> and the data in ordinal name order, nested objects too, and its hash is pinned (computed
    /// outside the code: <c>printf 'set-route\n{…}' | shasum -a 256</c>), so a renamed property or command type fails here.
    /// </summary>
    [Fact]
    public void Payload_IsTheCommandTypeAndTheDataWithAPinnedHash()
    {
        var command = new SetRouteCommand(_requestId, 6, 1, Route());

        Assert.Equal(
            "set-route\n{\"RequestId\":\"0199a1b2-c3d4-7e5f-8a9b-00000000000a\",\"Slot\":1,\"StationId\":6,\"Stops\":[" +
            "{\"Actions\":[{\"Amount\":\"all\",\"ItemId\":\"logs\",\"Kind\":\"take\"}],\"BuildingId\":2}," +
            "{\"Actions\":[{\"Amount\":\"all\",\"ItemId\":\"logs\",\"Kind\":\"put\"}],\"BuildingId\":5}]}",
            CommandPayload.Canonical(command));
        Assert.Equal("e4d2d3e26048210cd639f12a060b05276422e74c37b483d0892ddf8e08401c07", Convert.ToHexStringLower(CommandPayload.Hash(command)));
    }

    /// <summary>A route pays and refunds nothing, so it has no ledger reason (<c>06</c>: goods that move write no row).</summary>
    [Fact]
    public void LedgerReasons_AreNone()
    {
        var command = new SetRouteCommand(_requestId, 6, 1, Route());

        Assert.Equal(((LedgerReason?)null, (LedgerReason?)null), (command.CostReason, command.RefundReason));
    }

    /// <summary>
    /// Apply does exactly what the engine's rule does on a copy of the same city: a route set, a route cleared, and refusals (a slot above the
    /// level's 2 couriers, a building that is no courier station, a stop at a building the city doesn't have).
    /// </summary>
    /// <param name="stationId">The station.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="targetId">The route's second stop, or 0 for no stops.</param>
    [Theory]
    [InlineData(6, 1, 5)]
    [InlineData(6, 1, 0)]
    [InlineData(6, 3, 5)]
    [InlineData(3, 1, 5)]
    [InlineData(6, 1, 9)]
    public void Apply_DoesWhatTheEngineRuleDoes(int stationId, int slot, int targetId)
    {
        CityState byCommand = StationCity();
        CityState byRule = StationCity();
        CommandRouteStop?[] stops = targetId == 0 ? [] : Route(targetId);

        MutationResult commandResult = new SetRouteCommand(_requestId, stationId, slot, stops).Apply(byCommand, SettleFixture.Content);
        RouteStop[] engineStops = targetId == 0
            ? []
            :
            [
                new RouteStop(2, [new RouteAction(RouteActionKind.Take, Logs, Amount.Preset(new AmountPresetId("all")))]),
                new RouteStop(targetId, [new RouteAction(RouteActionKind.Put, Logs, Amount.Preset(new AmountPresetId("all")))]),
            ];
        MutationResult ruleResult = CourierRules.SetRoute(byRule, SettleFixture.Content, stationId, slot, engineStops);

        Assert.Equal(ruleResult.ToString(), commandResult.ToString());
        Assert.Equal(RoutesOf(byRule), RoutesOf(byCommand));
    }

    /// <summary>
    /// Through the pipeline the route is stored, the zero-length advance after the command sends courier 6/1 to its first stop at once, the
    /// ledger has no row (nothing was produced or paid) and the receipt is stored.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RouteOfTheWoodcuttersLogs_StoresItSendsTheCourierAndStoresTheReceipt()
    {
        InMemoryCityRepository repository = RepositoryWith(StationCity());
        var command = new SetRouteCommand(_requestId, 6, 1, Route());

        CommandResult result = await PipelineFixture.Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, command, PipelineFixture.Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        CityState stored = repository.Stored(PlayerId).State;
        Assert.Equal("6/1: 2[Take logs all] 5[Put logs all]", Assert.Single(RoutesOf(stored)));
        Courier courier = Assert.Single(stored.Couriers);
        Assert.Equal((CourierState.Walking, 1, (long?)180), (courier.State, courier.ToStop, courier.DepartedAt));
        Assert.Empty(repository.LastLedger!);
        CommandReceipt receipt = repository.StoredReceipt(PlayerId, _requestId)!;
        Assert.Equal((SetRouteCommand.Type, result.ResponseJson), (receipt.CommandType, receipt.ResponseJson));
    }

    /// <summary>The route of the tests: take all the logs at woodcutter 2, put them all into <paramref name="targetId"/> (sawmill 5).</summary>
    /// <param name="targetId">The second stop's building.</param>
    /// <returns>The stops.</returns>
    private static CommandRouteStop?[] Route(int targetId = 5) =>
        [new CommandRouteStop(2, [new CommandRouteAction("take", "logs", "all")]), new CommandRouteStop(targetId, [new CommandRouteAction("put", "logs", "all")])];

    /// <summary>A stop at the building that takes all its logs.</summary>
    /// <param name="buildingId">The building.</param>
    /// <returns>The stop.</returns>
    private static CommandRouteStop Take(int buildingId) => new(buildingId, [new CommandRouteAction("take", "logs", "all")]);

    /// <summary>The city's routes as text, one per slot.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The routes.</returns>
    private static string[] RoutesOf(CityState city) =>
        [.. city.Routes.Select(route => $"{route.StationId}/{route.Slot}: " + string.Join(" ", route.Stops.Select(stop =>
            $"{stop.BuildingId}[" + string.Join("; ", stop.Actions.Select(action => $"{action.Kind} {action.Item} {action.Amount}")) + "]")))];

    /// <summary>The two-sawmill city with courier station 6 at (1,1) and its one worker, courier 6/1 at the station with full stamina.</summary>
    /// <returns>The city at clock 180.</returns>
    private static CityState StationCity()
    {
        CityState sawmills = PipelineFixture.TwoSawmillsRepository().Stored(PlayerId).State;
        var station = new Building(6, new BuildingTypeId("courier_station"), 1, 1, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        return new CityState(
            sawmills.Clock,
            sawmills.Era,
            7,
            sawmills.Purse,
            [.. sawmills.Buildings, station],
            [],
            activeResearch: null,
            couriers: [new Courier(6, 1, CourierState.AtStation, 0, null, null, 14_400)]);
    }
}
