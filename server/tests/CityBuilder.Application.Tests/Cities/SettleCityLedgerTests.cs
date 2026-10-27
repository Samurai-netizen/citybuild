using CityBuilder.Application.Cities;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// The ledger rows of a settlement (<c>04</c> section 11, <c>06</c> <c>ledger_entries</c>) over the sawmill city of
/// <see cref="SettleFixture.SawmillRepository"/>, settled at clock 120 with 1 log: the woodcutter makes a log every 60 s from 180 on, and the
/// sawmill turns 2 logs into a plank every 120 s from 180 on.
/// </summary>
public sealed class SettleCityLedgerTests
{
    /// <summary>The sawmill's building id: it is the third building placed.</summary>
    private const int _sawmillId = 3;

    /// <summary>
    /// After an hour (to clock 3720) the woodcutter made 60 logs (180, 240, …, 3720), the sawmill took 60 (30 starts, 180 to 3660) and made
    /// 29 planks (300 to 3600). Per item in ordinal order there is one row per direction, produced before consumed, each balance running on
    /// from the quantity before: logs 1 → 61 → 1, planks 0 → 29. Logs made and used in one window never show a balance below 0.
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
                (Logs.Value, 60L, 61L, LedgerReason.SettlementProduced),
                (Logs.Value, -60L, 1L, LedgerReason.SettlementConsumed),
                (Planks.Value, 29L, 29L, LedgerReason.SettlementProduced),
            ],
            repository.LastLedger!.Select(entry => (entry.Item.Value, entry.Delta, entry.BalanceAfter, entry.Reason)));
        Assert.Equal(1, result.City.State.GetQuantity(Logs));
        Assert.Equal(29, result.City.State.GetQuantity(Planks));
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
    /// After 30 days the window made 481 logs (the last cycle started at the cap's end, clock 28920) and took 480 (240 sawmill starts, the last
    /// at 28860), leaving 2. The closing zero-length advance restarts the sawmill at once, and its 2 logs belong to the settlement: 482
    /// consumed, and the ledger ends logs at 0, the city's stock.
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
        Assert.Equal(240, result.Settlement.Produced[Planks]);
        Building sawmill = result.City.State.Buildings.Single(building => building.Id == _sawmillId);
        Assert.Equal((BuildingPhase.Producing, result.City.State.Clock), (sawmill.Phase, sawmill.PhaseStartedAt));
        Assert.Equal(
            [(Logs.Value, 481L, 482L), (Logs.Value, -482L, 0L), (Planks.Value, 240L, 240L)],
            repository.LastLedger!.Select(entry => (entry.Item.Value, entry.Delta, entry.BalanceAfter)));
        Assert.Equal(0, result.City.State.GetQuantity(Logs));
    }
}
