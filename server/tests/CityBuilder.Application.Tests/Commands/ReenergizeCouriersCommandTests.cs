using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.ReenergizeCouriers;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// <see cref="ReenergizeCouriersCommand"/> (CR-0008-T1 part H): intent only, so any shape is fine; its pinned canonical payload; no ledger
/// reason (nothing is paid or moved); that it applies the engine's rule (<see cref="CourierRules.ReenergizeCouriers"/>) and nothing else;
/// one run through <see cref="CommandPipeline"/> over the in-memory fakes. The test city is the two-sawmill city of
/// <see cref="PipelineFixture.TwoSawmillsRepository"/> with courier station 6 at (1,1): courier 6/1 resting with no stamina and 3 logs from
/// woodcutter 2, on the route woodcutter 2 → sawmill 5.
/// </summary>
public sealed class ReenergizeCouriersCommandTests
{
    /// <summary>The request id of the tests.</summary>
    private static readonly Guid _requestId = new("0199a1b2-c3d4-7e5f-8a9b-00000000000b");

    /// <summary>The command has no data, so its shape is always fine.</summary>
    [Fact]
    public void ValidateShape_IsAlwaysFine() => Assert.Null(new ReenergizeCouriersCommand(_requestId).ValidateShape());

    /// <summary>
    /// The canonical payload is <c>reenergize-couriers</c> and the request id, its only data, and its hash is pinned (computed outside the
    /// code: <c>printf 'reenergize-couriers\n{"RequestId":"…"}' | shasum -a 256</c>), so a renamed property or command type fails here.
    /// </summary>
    [Fact]
    public void Payload_IsTheCommandTypeAndTheRequestIdWithAPinnedHash()
    {
        var command = new ReenergizeCouriersCommand(_requestId);

        Assert.Equal("reenergize-couriers\n{\"RequestId\":\"0199a1b2-c3d4-7e5f-8a9b-00000000000b\"}", CommandPayload.Canonical(command));
        Assert.Equal("adb2ee05cf4edcec98a068d8cb017aa83e8b858ec0f244b410e76e941f2fb0e9", Convert.ToHexStringLower(CommandPayload.Hash(command)));
    }

    /// <summary>Re-energizing pays and refunds nothing, so it has no ledger reason.</summary>
    [Fact]
    public void LedgerReasons_AreNone()
    {
        var command = new ReenergizeCouriersCommand(_requestId);

        Assert.Equal(((LedgerReason?)null, (LedgerReason?)null), (command.CostReason, command.RefundReason));
    }

    /// <summary>Apply does exactly what the engine's rule does on a copy of the same city.</summary>
    [Fact]
    public void Apply_DoesWhatTheEngineRuleDoes()
    {
        CityState byCommand = RestingCity();
        CityState byRule = RestingCity();

        MutationResult commandResult = new ReenergizeCouriersCommand(_requestId).Apply(byCommand, SettleFixture.Content);
        MutationResult ruleResult = CourierRules.ReenergizeCouriers(byRule, SettleFixture.Content);

        Assert.Equal(ruleResult.ToString(), commandResult.ToString());
        Assert.Equal(Describe(byRule), Describe(byCommand));
    }

    /// <summary>
    /// Through the pipeline the courier's stamina is full again (the station level's 14 400 s), the zero-length advance after the command
    /// sends it to its first stop at once with the logs it kept, the ledger has no row and the receipt is stored.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RestingCourier_FillsItsStaminaSendsItOffAndStoresTheReceipt()
    {
        InMemoryCityRepository repository = RepositoryWith(RestingCity());
        var command = new ReenergizeCouriersCommand(_requestId);

        CommandResult result = await PipelineFixture.Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, command, PipelineFixture.Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        Courier courier = Assert.Single(repository.Stored(PlayerId).State.Couriers);
        Assert.Equal((CourierState.Walking, 1, (long?)180, 14_400L, 3L), (courier.State, courier.ToStop, courier.DepartedAt, courier.Stamina, courier.Carried));
        Assert.Empty(repository.LastLedger!);
        CommandReceipt receipt = repository.StoredReceipt(PlayerId, _requestId)!;
        Assert.Equal((ReenergizeCouriersCommand.Type, result.ResponseJson), (receipt.CommandType, receipt.ResponseJson));
    }

    /// <summary>The test city of the class summary, as a new object each time.</summary>
    /// <returns>The city at clock 180.</returns>
    private static CityState RestingCity()
    {
        CityState sawmills = PipelineFixture.TwoSawmillsRepository().Stored(PlayerId).State;
        var station = new Building(6, new BuildingTypeId("courier_station"), 1, 1, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        var city = new CityState(
            sawmills.Clock,
            sawmills.Era,
            7,
            sawmills.Purse,
            [.. sawmills.Buildings, station],
            [],
            activeResearch: null,
            couriers: [new Courier(6, 1, CourierState.Resting, 0, null, null, 0, [new CourierLoad(Logs, 2, 3)])],
            routes: [new Route(6, 1,
        [
            new RouteStop(2, [new RouteAction(RouteActionKind.Take, Logs, Amount.Preset(new AmountPresetId("all")))]),
            new RouteStop(5, [new RouteAction(RouteActionKind.Put, Logs, Amount.Preset(new AmountPresetId("all")))]),
        ])]);
        return city;
    }
}
