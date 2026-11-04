using CityBuilder.Application.Cities;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// A settlement in which trips arrive (CR-0008; <c>04</c> sections 16 and 18, <c>06</c> ledger): what a delivery to a project writes
/// (<c>SETTLEMENT_DELIVERED</c>, and <c>SETTLEMENT_REFUNDED</c> for the cycle an upgrade it starts ends), and that a trip whose whole load
/// comes back still changes and saves the city, although its report is empty. The cities are restored as stored, at clock 120.
/// </summary>
public sealed class SettleCityTripTests
{
    /// <summary>
    /// Trip 1 brings 3 logs to the producing sawmill, whose upgrade waits for 1 log: the log is delivered (used up), the upgrade starts and
    /// gives the cycle's 2 logs back into the sawmill's store, and the other 2 logs go into that store too. The ledger runs on from the city's
    /// total (5 in the woodcutter's store + 3 on the way = 8): refunded +2 → 10, then delivered −1 → 9, which is the total after.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_DeliveryStartsAWaitingUpgrade_WritesRefundedThenDelivered()
    {
        var state = new CityState(
            2 * StartClock,
            1,
            4,
            70,
            [
                new Building(1, new BuildingTypeId("cottage"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, Store(("logs", 5))),
                new Building(3, new BuildingTypeId("sawmill"), 2, 0, 1, 1, BuildingPhase.Producing, 120, 240, 0, 120, 120, 120, null, Store(("logs", 1))),
            ],
            [],
            activeResearch: null,
            trips: [new Trip(1, 2, 3, Logs, 3, 115, 125)],
            nextTripId: 2);
        InMemoryCityRepository repository = RepositoryWith(state);
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(10));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.Equal(
            [(Logs.Value, 2L, 10L, LedgerReason.SettlementRefunded), (Logs.Value, -1L, 9L, LedgerReason.SettlementDelivered)],
            repository.LastLedger!.Select(entry => (entry.Item.Value, entry.Delta, entry.BalanceAfter, entry.Reason)));
        Assert.Equal((1L, 2L, 2L), (result.Settlement.Delivered[Logs], result.Settlement.Moved[Logs], result.Settlement.Refunded[Logs]));
        Building sawmill = result.City.State.Buildings.Single(building => building.Id == 3);
        Assert.Equal((BuildingPhase.Upgrading, 125L, (long?)185L, 4L), (sawmill.Phase, sawmill.PhaseStartedAt, sawmill.PhaseEndsAt, sawmill.GetStock(Logs)));
        Assert.Equal(9, Total(result.City.State, Logs));
    }

    /// <summary>
    /// A trip to a store without room brings its whole load back: the report is empty and no ledger row is written, but the trip has ended
    /// and its worker is free, so the city is saved with a new version.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TripBringsItsWholeLoadBack_SavesTheCityWithoutLedgerRows()
    {
        var state = new CityState(
            2 * StartClock,
            1,
            4,
            70,
            [
                new Building(1, new BuildingTypeId("cottage"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, Store(("logs", 5))),
                new Building(3, new BuildingTypeId("sawmill"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, Store(("logs", 1000))),
            ],
            [],
            activeResearch: null,
            trips: [new Trip(1, 2, 3, Logs, 3, 115, 125)],
            nextTripId: 2);
        InMemoryCityRepository repository = RepositoryWith(state);
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(10));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.True(result.Settlement.IsEmpty);
        Assert.Empty(repository.LastLedger!);
        Assert.Equal(1, repository.SaveCount);
        Assert.Empty(result.City.State.Trips);
        Assert.Equal(8, result.City.State.Buildings.Single(building => building.Id == 2).GetStock(Logs));
    }
}
