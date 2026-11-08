using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.StartTrip;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// <see cref="StartTripCommand"/> (CR-0008): its shape check, its pinned canonical payload, its ledger reasons (none: goods only move), that it
/// applies the engine's rule (<see cref="TripRules.StartTrip"/>) and nothing else, and one run through <see cref="CommandPipeline"/> over the
/// in-memory fakes. The rule's checks are the engine's tests; the endpoint and PostgreSQL are the integration tests'.
/// </summary>
public sealed class StartTripCommandTests
{
    /// <summary>The request id of the tests.</summary>
    private static readonly Guid _requestId = new("0199a1b2-c3d4-7e5f-8a9b-00000000000a");

    /// <summary>Building ids below 1, or the same building twice, are a wrong shape: no city has them as a trip (OD-13).</summary>
    /// <param name="sourceId">The source id.</param>
    /// <param name="targetId">The target id.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData(2, 5, true)]
    [InlineData(0, 5, false)]
    [InlineData(2, 0, false)]
    [InlineData(-1, 5, false)]
    [InlineData(5, 5, false)]
    public void ValidateShape_BuildingIds_RefusesBelowOneAndTheSameTwice(int sourceId, int targetId, bool valid) =>
        Assert.Equal(valid, new StartTripCommand(_requestId, sourceId, targetId, "logs", "all").ValidateShape() is null);

    /// <summary>An item id that can't be a content id is a wrong shape; one of the right form is left to the content (<c>UNKNOWN_ITEM</c>).</summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData("logs", true)]
    [InlineData("no_such_item", true)]
    [InlineData("", false)]
    [InlineData("Logs", false)]
    [InlineData("logs ", false)]
    public void ValidateShape_ItemId_RefusesWhatCantBeAContentId(string itemId, bool valid) =>
        Assert.Equal(valid, new StartTripCommand(_requestId, 2, 5, itemId, "all").ValidateShape() is null);

    /// <summary>
    /// An amount is a preset id, a percentage from 1 % to 100 % or a number of 1 to 12 digits (<c>05</c> → Amounts); anything else is a wrong
    /// shape. A preset id the content lacks is left to the rule.
    /// </summary>
    /// <param name="amount">The amount's text.</param>
    /// <param name="valid">Whether the shape is fine.</param>
    [Theory]
    [InlineData("all", true)]
    [InlineData("no_such_preset", true)]
    [InlineData("50%", true)]
    [InlineData("100%", true)]
    [InlineData("12", true)]
    [InlineData("999999999999", true)]
    [InlineData("", false)]
    [InlineData("0%", false)]
    [InlineData("101%", false)]
    [InlineData("01", false)]
    [InlineData("1.5", false)]
    [InlineData("Half", false)]
    [InlineData("1000000000000", false)]
    public void ValidateShape_Amount_AcceptsTheThreeFormsOnly(string amount, bool valid) =>
        Assert.Equal(valid, new StartTripCommand(_requestId, 2, 5, "logs", amount).ValidateShape() is null);

    /// <summary>The message names the field, so the client knows what to fix.</summary>
    [Fact]
    public void ValidateShape_NamesTheField() =>
        Assert.StartsWith("amount ", new StartTripCommand(_requestId, 2, 5, "logs", "1.5%").ValidateShape(), StringComparison.Ordinal);

    /// <summary>
    /// The canonical payload is <c>start-trip</c> and the data in ordinal name order, and its hash is pinned (computed outside the code:
    /// <c>printf 'start-trip\n{…}' | shasum -a 256</c>), so a renamed property or command type fails here.
    /// </summary>
    [Fact]
    public void Payload_IsTheCommandTypeAndTheDataWithAPinnedHash()
    {
        var command = new StartTripCommand(_requestId, 2, 5, "logs", "half");

        Assert.Equal(
            "start-trip\n{\"Amount\":\"half\",\"ItemId\":\"logs\",\"RequestId\":\"0199a1b2-c3d4-7e5f-8a9b-00000000000a\",\"SourceId\":2,\"TargetId\":5}",
            CommandPayload.Canonical(command));
        Assert.Equal("72f2185b35575455cc570263549e88e6627c289fbd4d79e17d6081ca922cf867", Convert.ToHexStringLower(CommandPayload.Hash(command)));
    }

    /// <summary>A trip pays and refunds nothing, so it has no ledger reason (<c>06</c>: a move between stores and loads writes no row).</summary>
    [Fact]
    public void LedgerReasons_AreNone()
    {
        var command = new StartTripCommand(_requestId, 2, 5, "logs", "all");

        Assert.Equal(((LedgerReason?)null, (LedgerReason?)null), (command.CostReason, command.RefundReason));
    }

    /// <summary>
    /// Apply does exactly what the engine's rule does on a copy of the same city (the two-sawmill city a minute on, with 1 log in the
    /// woodcutter's store): a trip of it to sawmill 5, and refusals (a good the sawmill doesn't take, a building the city doesn't have).
    /// </summary>
    /// <param name="targetId">The target.</param>
    /// <param name="itemId">The good.</param>
    [Theory]
    [InlineData(5, "logs")]
    [InlineData(5, "coins")]
    [InlineData(9, "logs")]
    public void Apply_DoesWhatTheEngineRuleDoes(int targetId, string itemId)
    {
        CityState byCommand = CityWithALog();
        CityState byRule = CityWithALog();

        MutationResult commandResult = new StartTripCommand(_requestId, 2, targetId, itemId, "all").Apply(byCommand, SettleFixture.Content);
        MutationResult ruleResult = TripRules.StartTrip(byRule, SettleFixture.Content, 2, targetId, new ItemId(itemId), Amount.Preset(new AmountPresetId("all")));

        Assert.Equal(ruleResult.ToString(), commandResult.ToString());
        Assert.Equal(Describe(byRule), Describe(byCommand));
    }

    /// <summary>
    /// Through the pipeline a minute after the two-sawmill city was settled, the trip takes the woodcutter's log (made at 240) to sawmill 5 by
    /// the free tile (1,1): 10 s on open ground. The ledger has only the settlement's row of that log, no command row, and the receipt is stored.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TripOfTheWoodcuttersLog_StartsItWithoutALedgerRowAndStoresTheReceipt()
    {
        InMemoryCityRepository repository = PipelineFixture.TwoSawmillsRepository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(CycleSeconds));
        var command = new StartTripCommand(_requestId, 2, 5, "logs", "all");

        CommandResult result = await PipelineFixture.Pipeline(repository, clock)
            .ExecuteAsync(PlayerId, command, PipelineFixture.Writer(repository), TestContext.Current.CancellationToken);

        Assert.Equal(CommandOutcome.Succeeded, result.Outcome);
        Trip trip = Assert.Single(repository.Stored(PlayerId).State.Trips);
        Assert.Equal((1, 2, 5, Logs, 1L, 240L, 250L), (trip.Id, trip.SourceId, trip.TargetId, trip.Item, trip.Quantity, trip.DepartedAt, trip.ArrivesAt));
        LedgerEntry produced = Assert.Single(repository.LastLedger!);
        Assert.Equal((LedgerReason.SettlementProduced, 1L, 1L), (produced.Reason, produced.Delta, produced.BalanceAfter));
        CommandReceipt receipt = repository.StoredReceipt(PlayerId, _requestId)!;
        Assert.Equal((StartTripCommand.Type, result.ResponseJson), (receipt.CommandType, receipt.ResponseJson));
    }

    /// <summary>The two-sawmill city of <see cref="PipelineFixture.TwoSawmillsRepository"/> a minute on: the woodcutter has made its first log.</summary>
    private static CityState CityWithALog()
    {
        CityState city = PipelineFixture.TwoSawmillsRepository().Stored(PlayerId).State;
        Simulator.Advance(city, SettleFixture.Content, CycleSeconds);
        return city;
    }
}
