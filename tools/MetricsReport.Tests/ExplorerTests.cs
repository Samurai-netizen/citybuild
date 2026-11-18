namespace CityBuilder.MetricsReport.Tests;

/// <summary>
/// The checkpoint explorer (docs/plan/CR-0009-T2-PLAN.md §3.3, §3.4): every column of a row on a hand-made data set with known
/// answers, the zero denominators, the small-sample mark, the last stop and its tie, the default order and the detail blocks.
/// </summary>
public sealed class ExplorerTests
{
    /// <summary>Seconds in a day.</summary>
    private const long _day = 86_400;

    /// <summary>The report's clock in most tests: ten days after <see cref="TestData.Start"/>, so "left" means last seen before day 7.</summary>
    private static readonly DateTimeOffset _clock = TestData.Start.AddDays(10);

    /// <summary>
    /// The named checkpoints of the main data set: a woodcutter placed, a sawmill tried (any outcome), a second session, a research
    /// completed, and a research started (nobody did).
    /// </summary>
    private static readonly IReadOnlyList<Checkpoint> _named =
    [
        new("placed_woodcutter", "Placed a woodcutter", "command.place-building", "woodcutter", "accepted", null, 1),
        new("tried_sawmill", "Tried a sawmill", "command.place-building", "sawmill", null, null, 1),
        new("came_back", "Came back", "session.start", null, null, null, 2),
        new("research_done", "Completed a research", "research.completed", null, null, null, 1),
        new("started_research", "Started a research", "command.start-research", null, null, null, 1),
    ];

    /// <summary>
    /// Three players. Player 1 placed a woodcutter, was refused a sawmill and left. Player 2 placed two woodcutters, came back the
    /// next day, completed a research and stayed. Player 3 opened one session and left.
    /// </summary>
    /// <returns>The data set.</returns>
    private static MetricsData MainData() => new TestData()
        .Player(1, createdAtSecond: 0, lastSeenSecond: 100_000)
        .Player(2, createdAtSecond: 1_000, lastSeenSecond: 800_000)
        .Player(3, createdAtSecond: 2_000, lastSeenSecond: 2_500)
        .Event(1, 10, "session.start")
        .Event(1, 20, "command.place-building", "woodcutter", "accepted", cityClock: 5)
        .Event(1, 30, "command.place-building", "sawmill", "INSUFFICIENT_RESOURCE", cityClock: 8)
        .Event(2, 1_010, "session.start")
        .Event(2, 1_100, "command.place-building", "woodcutter", "accepted", cityClock: 50)
        .Event(2, 1_200, "command.place-building", "woodcutter", "accepted", cityClock: 60)
        .Event(2, 90_000, "session.start", cityClock: 70)
        .Event(2, 90_100, "research.completed", "basic_tools", cityClock: 300)
        .Event(3, 2_010, "session.start")
        .Build();

    /// <summary>The explorer of a data set.</summary>
    /// <param name="data">The data set.</param>
    /// <param name="named">The named checkpoints (the main ones when not given).</param>
    /// <param name="minSample">The least sample.</param>
    /// <param name="clock">The machine's clock (the main one when not given).</param>
    /// <returns>The rows.</returns>
    private static IReadOnlyList<ExplorerRow> Rows(
        MetricsData data, IReadOnlyList<Checkpoint>? named = null, int minSample = 5, DateTimeOffset? clock = null) =>
        Explorer.Build(ReportContext.Create(data, clock ?? _clock, leftAfterDays: 3, minSample), new CheckpointFile(named ?? _named, []));

    /// <summary>The row of a checkpoint.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="key">The checkpoint's id, or an unnamed one's title.</param>
    /// <returns>The row.</returns>
    private static ExplorerRow Row(IReadOnlyList<ExplorerRow> rows, string key) => Assert.Single(rows, r => r.Checkpoint.Key == key);

    /// <summary>Every column of a named row reached by two of three players, one of whom left.</summary>
    [Fact]
    public void Row_HasEveryColumn()
    {
        ExplorerRow row = Row(Rows(MainData()), "placed_woodcutter");

        Assert.Equal(2, row.Reached);
        Assert.Equal("66.7", row.ReachedShare.ToString());
        Assert.Equal(3, row.TimesTotal);
        Assert.Equal(1, row.MedianTimesPerPlayer);
        Assert.Equal(20, row.MedianSecondsToReach);
        Assert.Equal(5, row.MedianCityClockAtReach);
        Assert.Equal("50.0", row.LeftAfter.ToString());
        Assert.Equal("100.0", row.LeftWithout.ToString());
        Assert.Equal("-50.0", row.Difference.ToString());
        Assert.Equal(0, row.LastStop);
        Assert.Equal("0.0", row.LastStopShare.ToString());
        Assert.Equal(99_980, row.MedianSecondsAfterReach);
        Assert.Equal(0, row.MedianSessionsAfterReach);
    }

    /// <summary><c>atLeast</c> 2: the second session is the reach; the times count every match of every player.</summary>
    [Fact]
    public void Row_AtLeastTwo_ReachesOnTheSecondMatch()
    {
        ExplorerRow row = Row(Rows(MainData()), "came_back");

        Assert.Equal(1, row.Reached);
        Assert.Equal("33.3", row.ReachedShare.ToString());
        Assert.Equal(4, row.TimesTotal);
        Assert.Equal(2, row.MedianTimesPerPlayer);
        Assert.Equal(89_000, row.MedianSecondsToReach);
        Assert.Equal(70, row.MedianCityClockAtReach);
        Assert.Equal("0.0", row.LeftAfter.ToString());
        Assert.Equal("100.0", row.LeftWithout.ToString());
        Assert.Equal("-100.0", row.Difference.ToString());
        Assert.Equal(710_000, row.MedianSecondsAfterReach);
        Assert.Equal(0, row.MedianSessionsAfterReach);
    }

    /// <summary>A checkpoint nobody reached: no median, and the percentages over nobody print <c>—</c> instead of dividing by zero.</summary>
    [Fact]
    public void Row_NobodyReached_ShowsDashes()
    {
        ExplorerRow row = Row(Rows(MainData()), "started_research");

        Assert.Equal(0, row.Reached);
        Assert.Equal("0.0", row.ReachedShare.ToString());
        Assert.Equal(0, row.TimesTotal);
        Assert.Null(row.MedianTimesPerPlayer);
        Assert.Null(row.MedianSecondsToReach);
        Assert.Null(row.MedianCityClockAtReach);
        Assert.Equal("—", row.LeftAfter.ToString());
        Assert.Equal("66.7", row.LeftWithout.ToString());
        Assert.Equal("—", row.Difference.ToString());
        Assert.Null(row.MedianSecondsAfterReach);
        Assert.Null(row.MedianSessionsAfterReach);
    }

    /// <summary>A checkpoint everybody reached: "left without it" is over nobody.</summary>
    [Fact]
    public void Row_EverybodyReached_ShowsDashForLeftWithout()
    {
        ExplorerRow row = Row(Rows(MainData()), "session.start");

        Assert.False(row.Checkpoint.IsNamed);
        Assert.Equal(3, row.Reached);
        Assert.Equal("100.0", row.ReachedShare.ToString());
        Assert.Equal("66.7", row.LeftAfter.ToString());
        Assert.Equal("—", row.LeftWithout.ToString());
        Assert.Equal("—", row.Difference.ToString());
        Assert.Equal(0, row.MedianSessionsAfterReach);
    }

    /// <summary>
    /// Last stop among the named checkpoints: player 1's latest named reach is the sawmill; player 3 reached no named one. Among the
    /// unnamed ones it is each leaver's last event.
    /// </summary>
    [Fact]
    public void LastStop_NamedAndUnnamedApart()
    {
        IReadOnlyList<ExplorerRow> rows = Rows(MainData());

        Assert.Equal(1, Row(rows, "tried_sawmill").LastStop);
        Assert.Equal("50.0", Row(rows, "tried_sawmill").LastStopShare.ToString());
        Assert.Equal(0, Row(rows, "placed_woodcutter").LastStop);
        Assert.Equal(1, Row(rows, "command.place-building / sawmill / INSUFFICIENT_RESOURCE").LastStop);
        Assert.Equal(1, Row(rows, "session.start").LastStop);
        Assert.Equal(0, Row(rows, "command.place-building / woodcutter / accepted").LastStop);
    }

    /// <summary>Two reaches at the same time: the higher event id is the last stop; one event reaching two: the first in the file.</summary>
    [Fact]
    public void LastStop_Tie_HigherEventIdThenFileOrder()
    {
        MetricsData data = new TestData()
            .Player(1)
            .Event(1, 50, "research.completed", "basic_tools", id: 7)
            .Event(1, 50, "building.completed", "sawmill", value: 1, id: 3)
            .Player(2)
            .Event(2, 60, "session.start", id: 9)
            .Build();
        IReadOnlyList<Checkpoint> named =
        [
            new("built", "Built", "building.completed", null, null, null, 1),
            new("researched", "Researched", "research.completed", null, null, null, 1),
            new("session_any", "Any session", "session.start", null, null, null, 1),
            new("session_again", "Same session", "session.start", null, null, null, 1),
        ];

        IReadOnlyList<ExplorerRow> rows = Rows(data, named);

        Assert.Equal(1, Row(rows, "researched").LastStop);
        Assert.Equal(0, Row(rows, "built").LastStop);
        Assert.Equal(1, Row(rows, "session_any").LastStop);
        Assert.Equal(0, Row(rows, "session_again").LastStop);
    }

    /// <summary>Nobody left: every "left" percentage is 0, and the last stop's share is over nobody.</summary>
    [Fact]
    public void NobodyLeft()
    {
        MetricsData data = new TestData()
            .Player(1, lastSeenSecond: 9 * _day)
            .Player(2, lastSeenSecond: 9 * _day)
            .Event(1, 10, "session.start")
            .Build();

        ExplorerRow row = Row(Rows(data), "session.start");

        Assert.Equal("0.0", row.LeftAfter.ToString());
        Assert.Equal("0.0", row.LeftWithout.ToString());
        Assert.Equal("0.0", row.Difference.ToString());
        Assert.Equal(0, row.LastStop);
        Assert.Equal("—", row.LastStopShare.ToString());
    }

    /// <summary>Everybody left: every "left" percentage is 100.</summary>
    [Fact]
    public void EverybodyLeft()
    {
        MetricsData data = new TestData()
            .Player(1)
            .Player(2)
            .Event(1, 10, "session.start")
            .Build();

        ExplorerRow row = Row(Rows(data), "session.start");

        Assert.Equal("100.0", row.LeftAfter.ToString());
        Assert.Equal("100.0", row.LeftWithout.ToString());
        Assert.Equal("0.0", row.Difference.ToString());
        Assert.Equal(1, row.LastStop);
        Assert.Equal("50.0", row.LastStopShare.ToString());
    }

    /// <summary>A percentage over fewer players than the least sample is marked; the difference is marked when either side is.</summary>
    [Fact]
    public void SmallSample_IsMarkedPerPercentage()
    {
        ExplorerRow row = Row(Rows(MainData(), minSample: 2), "placed_woodcutter");

        Assert.False(row.ReachedShare.IsSmall);
        Assert.False(row.LeftAfter.IsSmall);
        Assert.True(row.LeftWithout.IsSmall);
        Assert.True(row.Difference.IsSmall);
        Assert.True(Row(Rows(MainData(), minSample: 5), "placed_woodcutter").ReachedShare.IsSmall);
    }

    /// <summary>"Now" is the later of the machine's clock and the newest last seen (a fast dev clock runs ahead).</summary>
    [Fact]
    public void Now_IsTheLaterOfClockAndNewestLastSeen()
    {
        MetricsData data = new TestData().Player(1, lastSeenSecond: 20 * _day).Player(2, lastSeenSecond: 16 * _day).Build();

        Assert.Equal(TestData.Start.AddDays(20), ReportContext.Create(data, TestData.Start, 3, 5).Now);
        Assert.Equal(TestData.Start.AddDays(30), ReportContext.Create(data, TestData.Start.AddDays(30), 3, 5).Now);
        Assert.True(ReportContext.Create(data, TestData.Start, 3, 5).HasLeft(data.Players[1]));
        Assert.False(ReportContext.Create(data, TestData.Start, 3, 5).HasLeft(data.Players[0]));
    }

    /// <summary>Percentages have one decimal, rounded half up (away from zero for a negative difference).</summary>
    /// <param name="part">The part.</param>
    /// <param name="whole">The whole.</param>
    /// <param name="expected">The printed percentage.</param>
    [Theory]
    [InlineData(1, 8, "12.5")]
    [InlineData(1, 16, "6.3")]
    [InlineData(2, 3, "66.7")]
    [InlineData(1, 3, "33.3")]
    [InlineData(1, 2_000, "0.1")]
    [InlineData(0, 0, "—")]
    public void Rate_RoundsHalfUp(long part, long whole, string expected) =>
        Assert.Equal(expected, Rate.Of(part, whole, 1).ToString());

    /// <summary>The difference in percentage points is computed from the four counts, not from the rounded percentages.</summary>
    [Fact]
    public void Rate_Difference_FromTheCounts()
    {
        Assert.Equal("-6.3", Rate.Difference(1, 16, 1, 8, 1).ToString());
        Assert.Equal("6.3", Rate.Difference(1, 8, 1, 16, 1).ToString());
        Assert.Equal("—", Rate.Difference(1, 8, 0, 0, 1).ToString());
    }

    /// <summary>The median is the lower middle value; none of nothing.</summary>
    [Fact]
    public void Median_IsTheLowerMiddle()
    {
        Assert.Equal(2, Stats.Median([4, 1, 3, 2]));
        Assert.Equal(3, Stats.Median([5, 3, 1]));
        Assert.Null(Stats.Median([]));
    }

    /// <summary>A checkpoint in no funnel has the same numbers as when a funnel names it.</summary>
    [Fact]
    public void Row_SameInAndOutOfAFunnel()
    {
        ReportContext context = ReportContext.Create(MainData(), _clock, 3, 5);
        ExplorerRow alone = Row(Explorer.Build(context, new CheckpointFile(_named, [])), "came_back");
        ExplorerRow inFunnel = Row(
            Explorer.Build(context, new CheckpointFile(_named, [new Funnel("F", ["placed_woodcutter", "came_back"])])), "came_back");

        Assert.Equal(alone with { Detail = null }, inFunnel with { Detail = null });
        Assert.Equal(alone.Detail!.NextEvents, inFunnel.Detail!.NextEvents);
        Assert.Equal(alone.Detail.LastEventsOfLeft, inFunnel.Detail.LastEventsOfLeft);
        Assert.Equal(alone.Detail.OtherCheckpointsOfLeft, inFunnel.Detail.OtherCheckpointsOfLeft);
    }

    /// <summary>The default order is last stop, descending; a tie keeps the named ones first, then the list order.</summary>
    [Fact]
    public void Rows_DefaultOrder_LastStopDescending()
    {
        string[] keys = [.. Rows(MainData()).Select(r => r.Checkpoint.Key)];

        Assert.Equal(
            [
                "tried_sawmill", "command.place-building / sawmill / INSUFFICIENT_RESOURCE", "session.start",
                "placed_woodcutter", "came_back", "research_done", "started_research",
                "command.place-building / woodcutter / accepted", "research.completed / basic_tools",
            ],
            keys);
    }

    /// <summary>
    /// The detail of a row: the next event after each player's reach; the last event of each player who left after it; the other
    /// named checkpoints those players reached. Each with players and the share.
    /// </summary>
    [Fact]
    public void Detail_NextLastAndOtherCheckpoints()
    {
        ExplorerDetail detail = Row(Rows(MainData()), "placed_woodcutter").Detail!;

        Assert.Equal(
            [
                new DetailCount("command.place-building / sawmill / INSUFFICIENT_RESOURCE", 1, Rate.Of(1, 2, 5)),
                new DetailCount("command.place-building / woodcutter / accepted", 1, Rate.Of(1, 2, 5)),
            ],
            detail.NextEvents);
        Assert.Equal([new DetailCount("command.place-building / sawmill / INSUFFICIENT_RESOURCE", 1, Rate.Of(1, 1, 5))], detail.LastEventsOfLeft);
        Assert.Equal([new DetailCount("Tried a sawmill", 1, Rate.Of(1, 1, 5))], detail.OtherCheckpointsOfLeft);
    }

    /// <summary>A detail list holds at most five, the most common first, a tie by title.</summary>
    [Fact]
    public void Detail_TopFive()
    {
        TestData builder = new();
        string[] next = ["a.x", "a.x", "b.x", "c.x", "d.x", "e.x", "f.x"];
        for (int player = 1; player <= next.Length; player++)
        {
            builder.Player(player, lastSeenSecond: 9 * _day).Event(player, 10, "session.start").Event(player, 20, next[player - 1]);
        }

        ExplorerDetail detail = Row(Rows(builder.Build(), []), "session.start").Detail!;

        Assert.Equal(["a.x", "b.x", "c.x", "d.x", "e.x"], detail.NextEvents.Select(d => d.Title));
        Assert.Equal(2, detail.NextEvents[0].Players);
        Assert.Equal("28.6", detail.NextEvents[0].Share.ToString());
    }

    /// <summary>Every named row has a detail; of the unnamed ones only the 50 most reached (a tie: the list order).</summary>
    [Fact]
    public void Detail_NamedAndTopFiftyUnnamed()
    {
        TestData builder = new TestData().Player(1).Player(2).Event(2, 5, "session.start");
        for (int i = 0; i < 52; i++)
        {
            builder.Event(1, 10 + i, "building.completed", $"b{i:D2}");
        }

        builder.Event(1, 100, "session.start");
        IReadOnlyList<ExplorerRow> rows = Rows(builder.Build(), [new("nobody", "Nobody", "command.start-research", null, null, null, 1)]);

        Assert.NotNull(Row(rows, "nobody").Detail);
        Assert.NotNull(Row(rows, "session.start").Detail);
        Assert.NotNull(Row(rows, "building.completed / b48").Detail);
        Assert.Null(Row(rows, "building.completed / b49").Detail);
        Assert.Equal(50, rows.Count(r => !r.Checkpoint.IsNamed && r.Detail is not null));
    }
}
