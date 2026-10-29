using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.StartResearch;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// <see cref="StartResearchCommand"/>: its shape check, its pinned canonical payload, its ledger reasons, that it applies the engine's rule
/// (<see cref="CityRules.StartResearch"/>) and nothing else, and one run through <see cref="CommandPipeline"/> over the in-memory fakes. The
/// rule's checks are the engine's tests; the endpoint and PostgreSQL are the integration tests'.
/// </summary>
public sealed class StartResearchCommandTests
{
    /// <summary>The request id of the tests.</summary>
    private static readonly Guid _requestId = new("0199a1b2-c3d4-7e5f-8a9b-00000000000a");

    /// <summary>
    /// A research id that can't be a content id is a wrong shape (missing, blank, a character the id format doesn't have), so it never locks
    /// the city (OD-13); an id of the right form is left to the content, even one the content doesn't have.
    /// </summary>
    /// <param name="researchId">The research id.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("\t", false)]
    [InlineData("sharp_axes", true)]
    [InlineData("no_such_research", true)]
    [InlineData("sharp-axes", false)]
    [InlineData("Sharp_axes", false)]
    [InlineData("sharp_axes ", false)]
    [InlineData("1sharp_axes", false)]
    [InlineData("shärp_axes", false)]
    public void ValidateShape_ResearchId_RefusesWhatCantBeAContentId(string researchId, bool valid)
    {
        string? error = new StartResearchCommand(_requestId, researchId).ValidateShape();

        Assert.Equal(valid, error is null);
    }

    /// <summary>
    /// The id format allows 64 characters: an id of 64 passes the shape check, one of 65 (or a megabyte of letters) is refused before the
    /// city is locked.
    /// </summary>
    /// <param name="length">The id's length.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    [InlineData(1_000_000, false)]
    public void ValidateShape_ResearchIdLength_AllowsTheIdFormatsSixtyFour(int length, bool valid)
    {
        string? error = new StartResearchCommand(_requestId, new string('a', length)).ValidateShape();

        Assert.Equal(valid, error is null);
    }

    /// <summary>The message names the field, so the client knows what to fix.</summary>
    [Fact]
    public void ValidateShape_NamesTheField()
    {
        string? error = new StartResearchCommand(_requestId, "Sharp Axes").ValidateShape();

        Assert.StartsWith("researchId ", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// The canonical payload is <c>start-research</c> and the data in ordinal name order, and its hash is pinned (computed outside the code:
    /// <c>printf 'start-research\n{…}' | shasum -a 256</c>), so a renamed property or command type, which would turn every stored receipt into
    /// a conflict, fails here.
    /// </summary>
    [Fact]
    public void Payload_IsTheCommandTypeAndTheDataWithAPinnedHash()
    {
        var command = new StartResearchCommand(_requestId, "sharp_axes");

        Assert.Equal(
            "start-research\n{\"RequestId\":\"0199a1b2-c3d4-7e5f-8a9b-00000000000a\",\"ResearchId\":\"sharp_axes\"}",
            CommandPayload.Canonical(command));
        Assert.Equal("28ada9f9329171f41c797452a4dc1b6e52cd0a573d8a757e3bbebb07ca565905", Convert.ToHexStringLower(CommandPayload.Hash(command)));
    }

    /// <summary>Starting research pays <c>COST_RESEARCH</c> and never refunds.</summary>
    [Fact]
    public void LedgerReasons_AreCostResearchAndNoRefund()
    {
        var command = new StartResearchCommand(_requestId, "sharp_axes");

        Assert.Equal((LedgerReason.CostResearch, (LedgerReason?)null), (command.CostReason, command.RefundReason));
    }

    /// <summary>
    /// Apply does exactly what the engine's rule does on a copy of the same city: starting <c>forestry</c> in the starter city, and refusals
    /// (a node the content doesn't have; <c>forestry</c> again in the city of <see cref="SettleFixture.Repository"/>, where it is running).
    /// </summary>
    /// <param name="researchId">The node to start.</param>
    /// <param name="running">Whether the city is the one with <c>forestry</c> running.</param>
    [Theory]
    [InlineData("forestry", false)]
    [InlineData("no_such_research", false)]
    [InlineData("forestry", true)]
    public void Apply_DoesWhatTheEngineRuleDoes(string researchId, bool running)
    {
        CityState byCommand = running ? SettleFixture.Repository().Stored(PlayerId).State : CityFactory.CreateStarterCity(SettleFixture.Content);
        CityState byRule = running ? SettleFixture.Repository().Stored(PlayerId).State : CityFactory.CreateStarterCity(SettleFixture.Content);

        MutationResult commandResult = new StartResearchCommand(_requestId, researchId).Apply(byCommand, SettleFixture.Content);
        MutationResult ruleResult = CityRules.StartResearch(byRule, SettleFixture.Content, new ResearchId(researchId));

        Assert.Equal(ruleResult.ToString(), commandResult.ToString());
        Assert.Equal(ruleResult.Spent, commandResult.Spent);
        Assert.Equal(Describe(byRule), Describe(byCommand));
    }

    /// <summary>
    /// Through the pipeline, the command starts <c>forestry</c> (ending its duration after the clock), writes its cost under
    /// <c>COST_RESEARCH</c> with the request id as reference, and stores a receipt of type <c>start-research</c> with the response.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_StartForestry_SavesTheResearchTheCostAndTheReceipt()
    {
        InMemoryCityRepository repository = RepositoryWith(CityFactory.CreateStarterCity(SettleFixture.Content));
        var command = new StartResearchCommand(_requestId, "forestry");

        CommandResult result = await PipelineFixture.Pipeline(repository, ClockAtLastSettled())
            .ExecuteAsync(PlayerId, command, PipelineFixture.Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        CityState stored = repository.Stored(PlayerId).State;
        Assert.Equal((Forestry, stored.Clock, stored.Clock + ResearchEndsAt),
            (stored.ActiveResearch!.Id, stored.ActiveResearch.StartedAt, stored.ActiveResearch.EndsAt));
        LedgerEntry cost = Assert.Single(repository.LastLedger!);
        Assert.Equal((LedgerReason.CostResearch, _requestId.ToString(), "coins", -10L), (cost.Reason, cost.RefId, cost.Item.Value, cost.Delta));
        CommandReceipt receipt = repository.StoredReceipt(PlayerId, _requestId)!;
        Assert.Equal((StartResearchCommand.Type, result.ResponseJson), (receipt.CommandType, receipt.ResponseJson));
    }
}
