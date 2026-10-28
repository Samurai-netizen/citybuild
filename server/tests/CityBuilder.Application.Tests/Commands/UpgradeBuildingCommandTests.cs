using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.UpgradeBuilding;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// <see cref="UpgradeBuildingCommand"/>: its shape check, its pinned canonical payload, its ledger reasons, that it applies the engine's rule
/// (<see cref="CityRules.UpgradeBuilding"/>) and nothing else, and one run through <see cref="CommandPipeline"/> over the in-memory fakes. The
/// rule's checks are the engine's tests; the endpoint and PostgreSQL are the integration tests'.
/// </summary>
public sealed class UpgradeBuildingCommandTests
{
    /// <summary>The request id of the tests.</summary>
    private static readonly Guid _requestId = new("0199a1b2-c3d4-7e5f-8a9b-00000000000a");

    /// <summary>Every integer is an id of the right form: one the city doesn't have is the rule's <c>BUILDING_NOT_FOUND</c>, not a shape error.</summary>
    /// <param name="buildingId">The building id.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ValidateShape_AnyBuildingId_IsFine(int buildingId)
    {
        Assert.Null(new UpgradeBuildingCommand(_requestId, buildingId).ValidateShape());
    }

    /// <summary>
    /// The canonical payload is <c>upgrade-building</c> and the data in ordinal name order, and its hash is pinned (computed outside the
    /// code: <c>printf 'upgrade-building\n{…}' | shasum -a 256</c>), so a renamed property or command type, which would turn every stored
    /// receipt into a conflict, fails here.
    /// </summary>
    [Fact]
    public void Payload_IsTheCommandTypeAndTheDataWithAPinnedHash()
    {
        var command = new UpgradeBuildingCommand(_requestId, 3);

        Assert.Equal(
            "upgrade-building\n{\"BuildingId\":3,\"RequestId\":\"0199a1b2-c3d4-7e5f-8a9b-00000000000a\"}",
            CommandPayload.Canonical(command));
        Assert.Equal("5f6aab05e942993e971fb7a9079ff695b73aa7c6cb468af8140d4a326761a360", Convert.ToHexStringLower(CommandPayload.Hash(command)));
    }

    /// <summary>Upgrading pays <c>COST_UPGRADE</c> and gives a running cycle's inputs back as <c>REFUND_UPGRADE</c>.</summary>
    [Fact]
    public void LedgerReasons_AreCostUpgradeAndRefundUpgrade()
    {
        var command = new UpgradeBuildingCommand(_requestId, 3);

        Assert.Equal((LedgerReason.CostUpgrade, (LedgerReason?)LedgerReason.RefundUpgrade), (command.CostReason, command.RefundReason));
    }

    /// <summary>
    /// Apply does exactly what the engine's rule does on a copy of the same city (<see cref="PipelineFixture.TwoSawmillsRepository"/>): an
    /// upgrade with a refund (sawmill 3, producing), one without (cottage 1), and refusals (an id the city doesn't have, 0, a negative id).
    /// </summary>
    /// <param name="buildingId">The building to upgrade.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_DoesWhatTheEngineRuleDoes(int buildingId)
    {
        CityState byCommand = PipelineFixture.TwoSawmillsRepository().Stored(PlayerId).State;
        CityState byRule = PipelineFixture.TwoSawmillsRepository().Stored(PlayerId).State;

        MutationResult commandResult = new UpgradeBuildingCommand(_requestId, buildingId).Apply(byCommand, SettleFixture.Content);
        MutationResult ruleResult = CityRules.UpgradeBuilding(byRule, SettleFixture.Content, buildingId);

        Assert.Equal(ruleResult.ToString(), commandResult.ToString());
        Assert.Equal(ruleResult.Spent, commandResult.Spent);
        Assert.Equal(ruleResult.Refunded, commandResult.Refunded);
        Assert.Equal(Describe(byRule), Describe(byCommand));
    }

    /// <summary>
    /// Through the pipeline, upgrading the producing sawmill 3 of <see cref="PipelineFixture.TwoSawmillsRepository"/> puts it into
    /// <c>Upgrading</c>, writes the refund under <c>REFUND_UPGRADE</c> and the cost under <c>COST_UPGRADE</c>, both with the request id as
    /// reference, and stores a receipt of type <c>upgrade-building</c> with the response.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_UpgradeProducingSawmill_SavesTheUpgradeTheRefundTheCostAndTheReceipt()
    {
        InMemoryCityRepository repository = PipelineFixture.TwoSawmillsRepository();
        var command = new UpgradeBuildingCommand(_requestId, 3);

        CommandResult result = await PipelineFixture.Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, command, PipelineFixture.Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        Building sawmill = repository.Stored(PlayerId).State.Buildings.Single(b => b.Id == 3);
        Assert.Equal((BuildingPhase.Upgrading, 1, 2), (sawmill.Phase, sawmill.Level, sawmill.TargetLevel));
        Assert.Equal(
            [(LedgerReason.RefundUpgrade, _requestId.ToString()), (LedgerReason.CostUpgrade, _requestId.ToString())],
            repository.LastLedger!.Where(entry => entry.RefType == "command").Select(entry => (entry.Reason, entry.RefId)));
        CommandReceipt receipt = repository.StoredReceipt(PlayerId, _requestId)!;
        Assert.Equal((UpgradeBuildingCommand.Type, result.ResponseJson), (receipt.CommandType, receipt.ResponseJson));
    }
}
