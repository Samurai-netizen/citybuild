using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.AssignWorkers;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// <see cref="AssignWorkersCommand"/>: its shape check, its pinned canonical payload, that it has no ledger reasons, that it applies the
/// engine's rule (<see cref="CityRules.AssignWorkers"/>) and nothing else, and one run through <see cref="CommandPipeline"/> over the in-memory
/// fakes. The rule's checks are the engine's tests; the endpoint and PostgreSQL are the integration tests'.
/// </summary>
public sealed class AssignWorkersCommandTests
{
    /// <summary>The request id of the tests.</summary>
    private static readonly Guid _requestId = new("0199a1b2-c3d4-7e5f-8a9b-00000000000a");

    /// <summary>
    /// An id below 1 (no city has one) and a negative count (no building can have one) are wrong shapes, so they never lock the city
    /// (OD-13); everything else passes, even an id the city doesn't have or a count above any max, which are the rule's.
    /// </summary>
    /// <param name="buildingId">The building id.</param>
    /// <param name="workers">The worker count.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, 3, true)]
    [InlineData(int.MaxValue, int.MaxValue, true)]
    [InlineData(0, 1, false)]
    [InlineData(-1, 1, false)]
    [InlineData(int.MinValue, 1, false)]
    [InlineData(1, -1, false)]
    [InlineData(1, int.MinValue, false)]
    [InlineData(0, -1, false)]
    public void ValidateShape_RefusesIdsBelowOneAndNegativeCounts(int buildingId, int workers, bool valid)
    {
        string? error = new AssignWorkersCommand(_requestId, buildingId, workers).ValidateShape();

        Assert.Equal(valid, error is null);
    }

    /// <summary>The message names the field that is wrong, the id first when both are.</summary>
    /// <param name="buildingId">The building id.</param>
    /// <param name="workers">The worker count.</param>
    /// <param name="field">The field the message starts with.</param>
    [Theory]
    [InlineData(0, 1, "buildingId ")]
    [InlineData(1, -1, "workers ")]
    [InlineData(0, -1, "buildingId ")]
    public void ValidateShape_NamesTheWrongField(int buildingId, int workers, string field)
    {
        string? error = new AssignWorkersCommand(_requestId, buildingId, workers).ValidateShape();

        Assert.StartsWith(field, error, StringComparison.Ordinal);
    }

    /// <summary>
    /// The canonical payload is <c>assign-workers</c> and the data in ordinal name order, and its hash is pinned (computed outside the code:
    /// <c>printf 'assign-workers\n{…}' | shasum -a 256</c>), so a renamed property or command type, which would turn every stored receipt into
    /// a conflict, fails here.
    /// </summary>
    [Fact]
    public void Payload_IsTheCommandTypeAndTheDataWithAPinnedHash()
    {
        var command = new AssignWorkersCommand(_requestId, 3, 2);

        Assert.Equal(
            "assign-workers\n{\"BuildingId\":3,\"RequestId\":\"0199a1b2-c3d4-7e5f-8a9b-00000000000a\",\"Workers\":2}",
            CommandPayload.Canonical(command));
        Assert.Equal("1b038df2b7ccd27289d7af3059fff8b186d86a19669ad3da42dbdcd513663eb7", Convert.ToHexStringLower(CommandPayload.Hash(command)));
    }

    /// <summary>Workers are no resource: assigning them never pays or gives back anything, so the command has no ledger reasons.</summary>
    [Fact]
    public void LedgerReasons_AreNone()
    {
        var command = new AssignWorkersCommand(_requestId, 3, 2);

        Assert.Equal((null, null), (command.CostReason, command.RefundReason));
    }

    /// <summary>
    /// Apply does exactly what the engine's rule does on a copy of the same city (<see cref="PipelineFixture.TwoSawmillsRepository"/>, one free
    /// worker, every max 1): freezing the producing sawmill 3, the same count again, unstaffing the woodcutter, and refusals (above the max, a
    /// cottage, an id the city doesn't have, 0, a negative count).
    /// </summary>
    /// <param name="buildingId">The building.</param>
    /// <param name="workers">The worker count.</param>
    [Theory]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(2, 0)]
    [InlineData(3, 2)]
    [InlineData(1, 1)]
    [InlineData(99, 1)]
    [InlineData(0, 1)]
    [InlineData(3, -1)]
    public void Apply_DoesWhatTheEngineRuleDoes(int buildingId, int workers)
    {
        CityState byCommand = PipelineFixture.TwoSawmillsRepository().Stored(PlayerId).State;
        CityState byRule = PipelineFixture.TwoSawmillsRepository().Stored(PlayerId).State;

        MutationResult commandResult = new AssignWorkersCommand(_requestId, buildingId, workers).Apply(byCommand, SettleFixture.Content);
        MutationResult ruleResult = CityRules.AssignWorkers(byRule, SettleFixture.Content, buildingId, workers);

        Assert.Equal(ruleResult.ToString(), commandResult.ToString());
        Assert.Equal(Describe(byRule), Describe(byCommand));
    }

    /// <summary>
    /// Through the pipeline, assigning 0 workers to the producing sawmill 3 of <see cref="PipelineFixture.TwoSawmillsRepository"/> freezes its
    /// cycle (no end) with the labor still to do kept, writes no ledger row for the command, and stores a receipt of type
    /// <c>assign-workers</c> with the response.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ZeroWorkersOnAProducingSawmill_FreezesItAndStoresTheReceipt()
    {
        InMemoryCityRepository repository = PipelineFixture.TwoSawmillsRepository();
        var command = new AssignWorkersCommand(_requestId, 3, 0);

        CommandResult result = await PipelineFixture.Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, command, PipelineFixture.Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        Building sawmill = repository.Stored(PlayerId).State.Buildings.Single(b => b.Id == 3);
        Assert.Equal((BuildingPhase.Producing, 0, (long?)null), (sawmill.Phase, sawmill.AssignedWorkers, sawmill.PhaseEndsAt));
        Assert.Equal(sawmill.CycleLabor, sawmill.LaborRemaining);
        Assert.DoesNotContain(repository.LastLedger!, entry => entry.RefType == "command");
        CommandReceipt receipt = repository.StoredReceipt(PlayerId, _requestId)!;
        Assert.Equal((AssignWorkersCommand.Type, result.ResponseJson), (receipt.CommandType, receipt.ResponseJson));
    }
}
