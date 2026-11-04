using CityBuilder.Application.Cities;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// The ledger rows of a settlement (<c>04</c> section 11, <c>06</c> <c>ledger_entries</c>) over the sawmill city of
/// <see cref="SettleFixture.SawmillRepository"/>, settled at clock 120: the woodcutter makes a log every 60 s from 180 on into its own store (1
/// log there), and the sawmill turns 2 logs of its own store (482) into a plank every 120 s, its running cycle ending at 240. Every balance is
/// the city's total of the item (CR-0008): 1 + 482 = 483 logs before.
/// </summary>
public sealed class SettleCityLedgerTests
{
    /// <summary>The sawmill's building id: it is the third building placed.</summary>
    private const int _sawmillId = 3;

    /// <summary>
    /// After an hour (to clock 3720) the woodcutter made 60 logs (180, 240, …, 3720), the sawmill took 60 (30 starts, 240 to 3720) and made
    /// 30 planks (240 to 3720). Per item in ordinal order there is one row per direction, produced before consumed, each balance running on
    /// from the total before: logs 483 → 543 → 483, planks 0 → 30. Logs made and used in one window never show a balance below 0.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_LogsMadeAndUsedInOneWindow_WritesProducedThenConsumedPerItem()
    {
        InMemoryCityRepository repository = SawmillRepository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromHours(1));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                (Logs.Value, 60L, 543L, LedgerReason.SettlementProduced),
                (Logs.Value, -60L, 483L, LedgerReason.SettlementConsumed),
                (Planks.Value, 30L, 30L, LedgerReason.SettlementProduced),
            ],
            repository.LastLedger!.Select(entry => (entry.Item.Value, entry.Delta, entry.BalanceAfter, entry.Reason)));
        Assert.Equal(483, Total(result.City.State, Logs));
        Assert.Equal(30, Total(result.City.State, Planks));
    }

    /// <summary>Every row of a settlement refers to it (<c>ref_type</c> <c>settlement</c>, <c>ref_id</c> its id); each settlement has its own id.</summary>
    [Fact]
    public async Task ExecuteAsync_AnyLedgerRow_ReferencesItsSettlement()
    {
        InMemoryCityRepository repository = SawmillRepository();
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);

        clock.Advance(TimeSpan.FromHours(1));
        SettleCityResult first = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        IReadOnlyList<LedgerEntry> firstRows = repository.LastLedger!;
        clock.Advance(TimeSpan.FromHours(1));
        SettleCityResult second = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.NotEqual(first.Settlement.Id, second.Settlement.Id);
        Assert.All(firstRows, entry => Assert.Equal((Settlement.LedgerRefType, first.Settlement.Id.ToString()), (entry.RefType, entry.RefId)));
        Assert.All(repository.LastLedger!, entry => Assert.Equal((Settlement.LedgerRefType, second.Settlement.Id.ToString()), (entry.RefType, entry.RefId)));
    }

    /// <summary>
    /// After 30 days the window made 481 logs (the woodcutter's last cycle started at the cap's end, clock 28920) and the sawmill took 480 of
    /// its store (240 starts, 240 to 28920), leaving 2 there, and made 241 planks. The closing zero-length advance restarts the sawmill at once,
    /// and those 2 logs belong to the settlement: 482 consumed, and the ledger ends logs at 482, the city's total (all in the woodcutter's store).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_LongerThanTheCap_LedgersWhatTheClosingAdvanceConsumes()
    {
        const long thirtyDays = 30 * 86_400;
        InMemoryCityRepository repository = SawmillRepository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(thirtyDays));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(481, result.Settlement.Produced[Logs]);
        Assert.Equal(482, result.Settlement.Consumed[Logs]);
        Assert.Equal(241, result.Settlement.Produced[Planks]);
        Building sawmill = result.City.State.Buildings.Single(building => building.Id == _sawmillId);
        Assert.Equal((BuildingPhase.Producing, result.City.State.Clock), (sawmill.Phase, sawmill.PhaseStartedAt));
        Assert.Equal(
            [(Logs.Value, 481L, 964L), (Logs.Value, -482L, 482L), (Planks.Value, 241L, 241L)],
            repository.LastLedger!.Select(entry => (entry.Item.Value, entry.Delta, entry.BalanceAfter)));
        Assert.Equal((482L, 0L), (Total(result.City.State, Logs), sawmill.GetStock(Logs)));
    }
}
