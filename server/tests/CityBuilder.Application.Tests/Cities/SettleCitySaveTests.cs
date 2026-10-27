using CityBuilder.Application.Cities;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// When a settle saves the city (<c>06</c>: <c>state_version + 1</c> only if anything changed): a settle that changes nothing commits
/// without a save; time passing, a new content version, or a cycle the closing zero-length advance starts is a change.
/// </summary>
public sealed class SettleCitySaveTests
{
    /// <summary>A settle with no time elapsed on a canonical city saves nothing: the transaction locks, loads and commits.</summary>
    [Fact]
    public async Task ExecuteAsync_NoTimeElapsed_CommitsWithoutASave()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.False(result.Saved);
        Assert.Equal(["begin", $"load {PlayerId}", "commit"], repository.Events);
        Assert.Equal(0, repository.SaveCount);
    }

    /// <summary>The second of two settles at the same instant changes nothing and isn't saved; the first is.</summary>
    [Fact]
    public async Task ExecuteAsync_TwiceAtTheSameInstant_SavesOnlyTheFirst()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        SettleCity settle = UseCase(repository, clock);
        clock.Advance(TimeSpan.FromSeconds(30));

        SettleCityResult first = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);
        SettleCityResult second = await settle.ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.True(first.Saved);
        Assert.False(second.Saved);
        Assert.Equal(0, second.Settlement.ElapsedSeconds);
        Assert.Equal(1, repository.SaveCount);
    }

    /// <summary>
    /// A settle that saved the city reports the next <c>state_version</c> (the city itself keeps the loaded one) and the server time as the
    /// clock gave it, not floored to the second.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SavedSettle_ReportsTheNextVersionAndTheClockReading()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();
        clock.Advance(TimeSpan.FromMilliseconds(30_500));

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.True(result.Saved);
        Assert.Equal(0, result.City.StateVersion);
        Assert.Equal(1, result.StateVersion);
        Assert.Equal(LastSettled.AddMilliseconds(30_500), result.ServerTime);
    }

    /// <summary>A settle that changed nothing and wasn't saved reports the version it loaded, because that is the stored one.</summary>
    [Fact]
    public async Task ExecuteAsync_UnsavedSettle_ReportsTheLoadedVersion()
    {
        InMemoryCityRepository repository = Repository();
        FakeTimeProvider clock = ClockAtLastSettled();

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.False(result.Saved);
        Assert.Equal(0, result.StateVersion);
        Assert.Equal(LastSettled, result.ServerTime);
    }

    /// <summary>A city last settled with other content is saved with the content version this settle ran with, even with no time elapsed.</summary>
    [Fact]
    public async Task ExecuteAsync_OtherContentVersion_SavesTheCurrentOne()
    {
        InMemoryCityRepository repository = RepositoryWith(Repository().Stored(PlayerId).State, contentVersion: "settle-test-0");
        FakeTimeProvider clock = ClockAtLastSettled();

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.True(result.Saved);
        Assert.Equal(SettleFixture.Content.ContentVersion, repository.Stored(PlayerId).ContentVersion);
    }

    /// <summary>
    /// A stored city that isn't canonical (an idle woodcutter with its worker, which could start) is saved even with no time elapsed: the
    /// zero-length advance starts the cycle, which takes no inputs, so the settlement's totals are empty, but the building changed.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_StoredCityNotCanonical_SavesTheCycleTheZeroLengthAdvanceStarts()
    {
        Building[] buildings =
        [
            new(1, new BuildingTypeId("cottage"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
        ];
        var state = new CityState(StartClock, era: 1, nextBuildingId: 3, [new(new ItemId("coins"), 80)], buildings, [], activeResearch: null);
        InMemoryCityRepository repository = RepositoryWith(state);
        FakeTimeProvider clock = ClockAtLastSettled();

        SettleCityResult result = await UseCase(repository, clock).ExecuteAsync(PlayerId, TestContext.Current.CancellationToken);

        Assert.True(result.Settlement.IsEmpty);
        Assert.True(result.Saved);
        Building woodcutter = result.City.State.Buildings.Single(building => building.Id == 2);
        Assert.Equal((BuildingPhase.Producing, StartClock), (woodcutter.Phase, woodcutter.PhaseStartedAt));
        Assert.Empty(repository.LastLedger!);
    }
}
