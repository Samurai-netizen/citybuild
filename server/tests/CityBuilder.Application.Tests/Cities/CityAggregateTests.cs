using CityBuilder.Application.Cities;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// The rules of <see cref="CityAggregate"/>: valid ids, version and content version, a completion clock for exactly the completed research,
/// and <see cref="CityAggregate.Advance"/> recording the clock of a research that completes.
/// </summary>
public sealed class CityAggregateTests
{
    /// <summary>A research id that the city completed before the tests.</summary>
    private static readonly ResearchId _masonry = new("masonry");

    /// <summary>A valid aggregate keeps every value it was given, and its completion clocks.</summary>
    [Fact]
    public void Constructor_ValidValues_KeepsThem()
    {
        CityState state = StateWithMasonry(clock: 500);

        var city = new CityAggregate(CityId, PlayerId, state, [new(_masonry, 120)], 1_790_000_000, 7, "v1");

        Assert.Equal(CityId, city.CityId);
        Assert.Equal(PlayerId, city.PlayerId);
        Assert.Same(state, city.State);
        Assert.Equal(new Dictionary<ResearchId, long> { [_masonry] = 120 }, city.ResearchCompletedAt);
        Assert.Equal(1_790_000_000, city.LastSettledUnixSec);
        Assert.Equal(7, city.StateVersion);
        Assert.Equal("v1", city.ContentVersion);
    }

    /// <summary>A completion clock at 0 or at the city clock is valid: both ends are inside the city's time.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(500L)]
    public void Constructor_CompletionClockAtEitherEnd_IsValid(long completedAt)
    {
        var city = new CityAggregate(CityId, PlayerId, StateWithMasonry(clock: 500), [new(_masonry, completedAt)], 0, 0, "v1");

        Assert.Equal(completedAt, city.ResearchCompletedAt[_masonry]);
    }

    /// <summary>
    /// The completion clocks must match the completed research: one missing, one for a research not completed, one listed twice, or one
    /// before 0 or after the city clock is rejected.
    /// </summary>
    [Fact]
    public void Constructor_CompletionClocksNotMatchingTheState_Throws()
    {
        CityState state = StateWithMasonry(clock: 500);

        Assert.Throws<ArgumentException>(() => new CityAggregate(CityId, PlayerId, state, [], 0, 0, "v1"));
        Assert.Throws<ArgumentException>(
            () => new CityAggregate(CityId, PlayerId, state, [new(_masonry, 1), new(Forestry, 1)], 0, 0, "v1"));
        Assert.Throws<ArgumentException>(
            () => new CityAggregate(CityId, PlayerId, state, [new(_masonry, 1), new(_masonry, 2)], 0, 0, "v1"));
        Assert.Throws<ArgumentException>(() => new CityAggregate(CityId, PlayerId, state, [new(_masonry, -1)], 0, 0, "v1"));
        Assert.Throws<ArgumentException>(() => new CityAggregate(CityId, PlayerId, state, [new(_masonry, 501)], 0, 0, "v1"));
    }

    /// <summary>An empty city or player id, a negative state version or a blank content version is rejected.</summary>
    [Fact]
    public void Constructor_InvalidIdsOrVersions_Throws()
    {
        CityState state = StateWithMasonry(clock: 500);
        KeyValuePair<ResearchId, long>[] clocks = [new(_masonry, 1)];

        Assert.Throws<ArgumentException>(() => new CityAggregate(Guid.Empty, PlayerId, state, clocks, 0, 0, "v1"));
        Assert.Throws<ArgumentException>(() => new CityAggregate(CityId, Guid.Empty, state, clocks, 0, 0, "v1"));
        Assert.Throws<ArgumentException>(() => new CityAggregate(CityId, PlayerId, state, clocks, 0, -1, "v1"));
        Assert.Throws<ArgumentException>(() => new CityAggregate(CityId, PlayerId, state, clocks, 0, 0, " "));
    }

    /// <summary>
    /// <see cref="CityAggregate.Advance"/> records a completed research at its end, also when the advance goes far beyond it, and keeps the
    /// clocks of research completed before.
    /// </summary>
    [Fact]
    public void Advance_PastTheEndOfTheRunningResearch_RecordsItsEndAsTheCompletionClock()
    {
        var state = new CityState(500, 1, 1, [], [], [_masonry], new ActiveResearch(Forestry, 400, 1_000));
        var city = new CityAggregate(CityId, PlayerId, state, [new(_masonry, 120)], 0, 0, "v1");

        SettlementReport report = city.Advance(SettleFixture.Content, 10_000);

        Assert.Equal([Forestry], report.CompletedResearch);
        Assert.Equal(new Dictionary<ResearchId, long> { [Forestry] = 1_000, [_masonry] = 120 }, city.ResearchCompletedAt);
    }

    /// <summary>An advance that completes no research records nothing.</summary>
    [Fact]
    public void Advance_BeforeTheEndOfTheRunningResearch_RecordsNothing()
    {
        var state = new CityState(500, 1, 1, [], [], [], new ActiveResearch(Forestry, 400, 1_000));
        var city = new CityAggregate(CityId, PlayerId, state, [], 0, 0, "v1");

        city.Advance(SettleFixture.Content, 499);

        Assert.Empty(city.ResearchCompletedAt);
        Assert.NotNull(city.State.ActiveResearch);
    }

    /// <summary>A city at clock <paramref name="clock"/> with no buildings or stock that completed <c>masonry</c> and runs nothing.</summary>
    private static CityState StateWithMasonry(long clock) => new(clock, 1, 1, [], [], [_masonry], activeResearch: null);
}
