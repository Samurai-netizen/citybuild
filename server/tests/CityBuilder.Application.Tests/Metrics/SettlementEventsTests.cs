using CityBuilder.Application.Cities;
using CityBuilder.Application.Metrics;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Metrics;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Metrics;

/// <summary>
/// <see cref="SettlementEvents.Of"/> (CR-0009): a settlement's completions as player events, one <c>research.completed</c> per research
/// node (detail the research id) and one <c>building.completed</c> per construction or upgrade (detail the type, value the level it now
/// has), all at the settled city clock and the request's server time. The settlements are real ones of <see cref="SettleFixture"/>'s city.
/// </summary>
public sealed class SettlementEventsTests
{
    /// <summary>
    /// A long offline window that completes the research and the manor gives two events, the research's first, each once, at the clock the
    /// city was settled to (not the second each finished: the limitation <c>06</c> names).
    /// </summary>
    [Fact]
    public async Task Of_ThirtyDaysAway_GivesOneEventPerCompletion()
    {
        SettleCityResult result = await SettleAfterAsync(Repository(), 30 * 86_400);

        IReadOnlyList<PlayerEvent> events = SettlementEvents.Of(result.City, result.Settlement, result.ServerTime);

        long clock = result.City.State.Clock;
        Assert.Equal(
            [
                (PlayerId, PlayerEventTypes.ResearchCompleted, (string?)Forestry.Value, (string?)null, (long?)null, clock, result.ServerTime),
                (PlayerId, PlayerEventTypes.BuildingCompleted, "manor", null, 1L, clock, result.ServerTime),
            ],
            events.Select(e => (e.PlayerId, e.EventType, e.Detail, e.Outcome, e.Value, e.CityClock, e.OccurredAt)));
    }

    /// <summary>A completed upgrade gives the level the building rose to.</summary>
    [Fact]
    public async Task Of_UpgradeCompleted_ValueIsTheNewLevel()
    {
        SettleCityResult result = await SettleAfterAsync(RepositoryWith(UpgradingSawmill()), 600);

        PlayerEvent completed = Assert.Single(SettlementEvents.Of(result.City, result.Settlement, result.ServerTime));

        Assert.Equal((PlayerEventTypes.BuildingCompleted, "sawmill", (long?)2), (completed.EventType, completed.Detail, completed.Value));
    }

    /// <summary>A settlement that completes nothing (production only) gives no event.</summary>
    [Fact]
    public async Task Of_NothingCompleted_IsEmpty()
    {
        SettleCityResult result = await SettleAfterAsync(Repository(), 3600);

        Assert.Empty(SettlementEvents.Of(result.City, result.Settlement, result.ServerTime));
    }

    /// <summary>A null argument is refused.</summary>
    [Fact]
    public async Task Of_Null_Throws()
    {
        SettleCityResult result = await SettleAfterAsync(Repository(), 0);

        Assert.Throws<ArgumentNullException>(() => SettlementEvents.Of(null!, result.Settlement, result.ServerTime));
        Assert.Throws<ArgumentNullException>(() => SettlementEvents.Of(result.City, null!, result.ServerTime));
    }

    /// <summary>Settles the repository's city once, the given seconds after its last settlement.</summary>
    /// <param name="repository">The city.</param>
    /// <param name="seconds">The seconds away.</param>
    /// <returns>The settle's result.</returns>
    private static async Task<SettleCityResult> SettleAfterAsync(InMemoryCityRepository repository, long seconds)
    {
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromSeconds(seconds));
        return await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// <see cref="SawmillState"/> with sawmill 3 upgrading from level 1 to 2 (every delivery made), its upgrade ending at clock 180, a minute
    /// after the last settlement.
    /// </summary>
    /// <returns>A new state.</returns>
    private static CityState UpgradingSawmill()
    {
        CityState state = SawmillState();
        Building[] buildings =
        [
            .. state.Buildings.Where(building => building.Id != 3),
            new Building(3, new BuildingTypeId("sawmill"), 2, 0, 1, 1, BuildingPhase.Upgrading, 120, 180, 2, 0, 0, 0, Store(("logs", SawmillLogs))),
        ];
        return new CityState(state.Clock, state.Era, state.NextBuildingId, state.Purse, buildings, [], activeResearch: null);
    }
}
