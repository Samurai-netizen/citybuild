namespace CityBuilder.MetricsReport.Tests;

/// <summary>
/// The analysis model (docs/plan/CR-0009-T2-PLAN.md §3.3): what an event must have to match a checkpoint, when a player reached it
/// and with which event, the unnamed checkpoints built from the data, and the order of a player's timeline.
/// </summary>
public sealed class AnalysisModelTests
{
    /// <summary>A named checkpoint as the file would give it.</summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="detail">The detail, or any.</param>
    /// <param name="outcome">The outcome, or any.</param>
    /// <param name="minValue">The least value, or any.</param>
    /// <param name="atLeast">How many matches reach it.</param>
    /// <returns>The checkpoint, with the id <c>c</c>.</returns>
    private static ReportCheckpoint Named(string eventType, string? detail = null, string? outcome = null, long? minValue = null, int atLeast = 1) =>
        ReportCheckpoint.Named(new Checkpoint("c", "C", eventType, detail, outcome, minValue, atLeast));

    /// <summary>An event of player 1 with an id and a time that don't matter.</summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="detail">The detail.</param>
    /// <param name="outcome">The outcome.</param>
    /// <param name="value">The value.</param>
    /// <returns>The event.</returns>
    private static MetricsEvent Event(string eventType, string? detail = null, string? outcome = null, long? value = null) =>
        new(1, TestData.PlayerId(1), eventType, detail, outcome, value, 0, TestData.Start);

    /// <summary>The event type must be the same; a checkpoint that names no detail or outcome takes any.</summary>
    [Fact]
    public void Matches_SameTypeAndAnyDetail()
    {
        ReportCheckpoint checkpoint = Named("command.place-building");

        Assert.True(CheckpointMatcher.Matches(checkpoint, Event("command.place-building", "woodcutter", "accepted")));
        Assert.True(CheckpointMatcher.Matches(checkpoint, Event("command.place-building", "sawmill", "INSUFFICIENT_RESOURCE")));
        Assert.False(CheckpointMatcher.Matches(checkpoint, Event("command.upgrade-building", "woodcutter", "accepted")));
    }

    /// <summary>A named detail and outcome must be equal, with case.</summary>
    [Fact]
    public void Matches_NamedDetailAndOutcomeMustBeEqual()
    {
        ReportCheckpoint checkpoint = Named("command.place-building", "woodcutter", "accepted");

        Assert.True(CheckpointMatcher.Matches(checkpoint, Event("command.place-building", "woodcutter", "accepted")));
        Assert.False(CheckpointMatcher.Matches(checkpoint, Event("command.place-building", "sawmill", "accepted")));
        Assert.False(CheckpointMatcher.Matches(checkpoint, Event("command.place-building", "woodcutter", "INSUFFICIENT_RESOURCE")));
        Assert.False(CheckpointMatcher.Matches(checkpoint, Event("command.place-building", null, "accepted")));
        Assert.False(CheckpointMatcher.Matches(checkpoint, Event("command.place-building", "Woodcutter", "accepted")));
    }

    /// <summary>A named least value needs a value at or above it; an event without a value doesn't match.</summary>
    [Fact]
    public void Matches_MinValueNeedsAValueAtOrAboveIt()
    {
        ReportCheckpoint checkpoint = Named("building.completed", "sawmill", minValue: 2);

        Assert.True(CheckpointMatcher.Matches(checkpoint, Event("building.completed", "sawmill", value: 2)));
        Assert.True(CheckpointMatcher.Matches(checkpoint, Event("building.completed", "sawmill", value: 3)));
        Assert.False(CheckpointMatcher.Matches(checkpoint, Event("building.completed", "sawmill", value: 1)));
        Assert.False(CheckpointMatcher.Matches(checkpoint, Event("building.completed", "sawmill")));
    }

    /// <summary>An unnamed checkpoint matches its exact triple: a missing detail or outcome matches only a missing one.</summary>
    [Fact]
    public void Matches_UnnamedNeedsTheExactTriple()
    {
        ReportCheckpoint checkpoint = ReportCheckpoint.Unnamed("command.set-route", null, "accepted");

        Assert.True(CheckpointMatcher.Matches(checkpoint, Event("command.set-route", null, "accepted", 7)));
        Assert.False(CheckpointMatcher.Matches(checkpoint, Event("command.set-route", "courier_station", "accepted")));
        Assert.False(CheckpointMatcher.Matches(checkpoint, Event("command.set-route", null, null)));
    }

    /// <summary>Every distinct triple in the data is one unnamed checkpoint, in ordinal order, titled with its parts.</summary>
    [Fact]
    public void Unnamed_OnePerDistinctTriple()
    {
        MetricsData data = new TestData()
            .Player(1).Player(2)
            .Event(1, 10, "session.start")
            .Event(2, 10, "session.start")
            .Event(1, 20, "command.place-building", "woodcutter", "accepted")
            .Event(2, 20, "command.place-building", "woodcutter", "accepted")
            .Event(1, 30, "command.place-building", "woodcutter", "INSUFFICIENT_RESOURCE")
            .Event(1, 40, "command.set-route", null, "accepted")
            .Event(1, 50, "building.completed", "woodcutter", null, 1)
            .Event(1, 60, "building.completed", "woodcutter", null, 2)
            .Build();

        IReadOnlyList<ReportCheckpoint> unnamed = CheckpointMatcher.Unnamed(data.Events);

        Assert.Equal(
            [
                "building.completed / woodcutter",
                "command.place-building / woodcutter / INSUFFICIENT_RESOURCE",
                "command.place-building / woodcutter / accepted",
                "command.set-route / — / accepted",
                "session.start",
            ],
            unnamed.Select(c => c.Title));
        Assert.All(unnamed, c => Assert.False(c.IsNamed));
        Assert.All(unnamed, c => Assert.Equal(1, c.AtLeast));
        Assert.Equal(unnamed.Count, unnamed.Select(c => c.Key).Distinct().Count());
    }

    /// <summary>All checkpoints: the named ones in file order, then the unnamed ones; keys are unique.</summary>
    [Fact]
    public void All_NamedThenUnnamed()
    {
        Checkpoint[] named =
        [
            new("zeta", "Z", "session.start", null, null, null, 2),
            new("alpha", "A", "command.place-building", "woodcutter", "accepted", null, 1),
        ];
        MetricsData data = new TestData().Player(1).Event(1, 10, "session.start").Build();

        IReadOnlyList<ReportCheckpoint> all = CheckpointMatcher.All(named, data.Events);

        Assert.Equal(["zeta", "alpha"], all.Take(2).Select(c => c.Key));
        Assert.True(all[0].IsNamed);
        Assert.Equal(2, all[0].AtLeast);
        Assert.Equal("session.start", Assert.Single(all.Skip(2)).Title);
        Assert.Equal(all.Count, all.Select(c => c.Key).Distinct().Count());
    }

    /// <summary>A player's timeline holds only their events, in (created_at, id) order: a tie on time puts the lower id first.</summary>
    [Fact]
    public void Timeline_OrdersByTimeThenId()
    {
        MetricsData data = new TestData()
            .Player(2, createdAtSecond: 5).Player(1)
            .Event(1, 30, "session.start", id: 10)
            .Event(1, 20, "session.start", id: 11)
            .Event(2, 20, "session.start", id: 12)
            .Event(1, 20, "player.created", id: 9)
            .Build();

        IReadOnlyList<PlayerTimeline> timelines = PlayerTimeline.Build(data);

        Assert.Equal([TestData.PlayerId(1), TestData.PlayerId(2)], timelines.Select(t => t.Player.Id));
        Assert.Equal([9L, 11L, 10L], timelines[0].Events.Select(e => e.Id));
        Assert.Equal([12L], timelines[1].Events.Select(e => e.Id));
    }

    /// <summary>The reach is the atLeast-th match in timeline order; fewer matches: not reached; the count is every match.</summary>
    [Fact]
    public void Reach_IsTheAtLeastThMatch()
    {
        MetricsData data = new TestData()
            .Player(1)
            .Event(1, 30, "session.start", id: 3)
            .Event(1, 10, "session.start", id: 1)
            .Event(1, 20, "command.place-building", "woodcutter", "accepted", id: 2)
            .Event(1, 30, "session.start", id: 4)
            .Build();
        PlayerTimeline timeline = Assert.Single(PlayerTimeline.Build(data));

        Assert.Equal(1L, timeline.Reach(Named("session.start"))?.Id);
        Assert.Equal(3L, timeline.Reach(Named("session.start", atLeast: 2))?.Id);
        Assert.Equal(4L, timeline.Reach(Named("session.start", atLeast: 3))?.Id);
        Assert.Null(timeline.Reach(Named("session.start", atLeast: 4)));
        Assert.Equal(3, timeline.Count(Named("session.start", atLeast: 4)));
        Assert.Null(timeline.Reach(Named("research.completed")));
        Assert.Equal(0, timeline.Count(Named("research.completed")));
    }

    /// <summary>A player with no events has an empty timeline and reaches nothing; events of a player not in the data are left out.</summary>
    [Fact]
    public void Timeline_PlayerWithoutEvents_ReachesNothing()
    {
        MetricsData data = new TestData().Player(1).Event(2, 10, "session.start").Build();

        PlayerTimeline timeline = Assert.Single(PlayerTimeline.Build(data));

        Assert.Empty(timeline.Events);
        Assert.Null(timeline.Reach(Named("session.start")));
    }
}
