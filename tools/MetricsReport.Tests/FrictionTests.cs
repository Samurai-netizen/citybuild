namespace CityBuilder.MetricsReport.Tests;

/// <summary>
/// The friction section (docs/plan/CR-0009-T2-PLAN.md §3.4): refused commands by command and error code, the last event of each player
/// who left (top ten), and the players who left with a refusal in their last session.
/// </summary>
public sealed class FrictionTests
{
    /// <summary>The report's clock: ten days after the start, so "left" means last seen before day 7.</summary>
    private static readonly DateTimeOffset _clock = TestData.Start.AddDays(10);

    /// <summary>
    /// Four players, three of whom left. Player 1 was refused a sawmill twice, came back, placed a woodcutter and was refused an
    /// upgrade last. Player 2 was refused a sawmill, came back and placed a woodcutter last. Player 3 stayed, refused once. Player 4
    /// left with no events.
    /// </summary>
    /// <returns>The friction.</returns>
    private static FrictionReport Report() => Friction.Build(ReportContext.Create(
        new TestData()
            .Player(1, lastSeenSecond: 1_000)
            .Player(2, lastSeenSecond: 1_000)
            .Player(3, lastSeenSecond: 9 * 86_400)
            .Player(4)
            .Event(1, 10, "session.start")
            .Event(1, 20, "command.place-building", "sawmill", "INSUFFICIENT_RESOURCE")
            .Event(1, 30, "command.place-building", "sawmill", "INSUFFICIENT_RESOURCE")
            .Event(1, 500, "session.start")
            .Event(1, 600, "command.place-building", "woodcutter", "accepted")
            .Event(1, 700, "command.upgrade-building", "sawmill", "INSUFFICIENT_RESOURCE")
            .Event(2, 10, "session.start")
            .Event(2, 20, "command.place-building", "sawmill", "INSUFFICIENT_RESOURCE")
            .Event(2, 300, "session.start")
            .Event(2, 400, "command.place-building", "woodcutter", "accepted")
            .Event(3, 10, "session.start")
            .Event(3, 20, "command.upgrade-building", null, "INVALID_TARGET")
            .Build(),
        _clock, 3, 5));

    /// <summary>Refusals by command and code: players hit (count, % of the population) and times; accepted commands are not refusals.</summary>
    [Fact]
    public void Refusals_ByCommandAndCode()
    {
        IReadOnlyList<FrictionRow> rows = Report().Refusals;

        Assert.Equal(
            ["command.place-building INSUFFICIENT_RESOURCE", "command.upgrade-building INSUFFICIENT_RESOURCE", "command.upgrade-building INVALID_TARGET"],
            rows.Select(r => $"{r.EventType} {r.Outcome}"));
        Assert.Equal([2, 1, 1], rows.Select(r => r.Players));
        Assert.Equal(["50.0", "25.0", "25.0"], rows.Select(r => r.Share.ToString()));
        Assert.Equal([3L, 1L, 1L], rows.Select(r => r.Times));
    }

    /// <summary>The last event of each player who left, grouped by title, as a share of those who left.</summary>
    [Fact]
    public void LastEvents_OfThePlayersWhoLeft()
    {
        IReadOnlyList<DetailCount> rows = Report().LastEvents;

        Assert.Equal(
            ["command.place-building / woodcutter / accepted", "command.upgrade-building / sawmill / INSUFFICIENT_RESOURCE"],
            rows.Select(r => r.Title));
        Assert.Equal([1, 1], rows.Select(r => r.Players));
        Assert.Equal(["33.3", "33.3"], rows.Select(r => r.Share.ToString()));
    }

    /// <summary>The last events are capped at ten, the most players first.</summary>
    [Fact]
    public void LastEvents_TopTen()
    {
        var data = new TestData();
        for (int player = 1; player <= 12; player++)
        {
            data.Player(player).Event(player, 10, "command.place-building", $"building_{player:D2}", "accepted");
        }

        data.Player(13).Event(13, 10, "command.place-building", "building_12", "accepted");

        IReadOnlyList<DetailCount> rows = Friction.Build(ReportContext.Create(data.Build(), _clock, 3, 5)).LastEvents;

        Assert.Equal(Friction.LastEventCount, rows.Count);
        Assert.Equal("command.place-building / building_12 / accepted", rows[0].Title);
        Assert.Equal(2, rows[0].Players);
    }

    /// <summary>Leavers whose last session (from their last <c>session.start</c>) held a refusal, by command and code.</summary>
    [Fact]
    public void LastSessionRefusals_OnlyTheLastSession()
    {
        FrictionRow row = Assert.Single(Report().LastSessionRefusals);

        Assert.Equal(("command.upgrade-building", "INSUFFICIENT_RESOURCE"), (row.EventType, row.Outcome));
        Assert.Equal(1, row.Players);
        Assert.Equal("33.3", row.Share.ToString());
        Assert.Equal(1L, row.Times);
    }

    /// <summary>A leaver with no <c>session.start</c>: all their events are the last session.</summary>
    [Fact]
    public void LastSessionRefusals_NoSessionStart_EveryEventCounts()
    {
        MetricsData data = new TestData()
            .Player(1)
            .Event(1, 10, "command.place-building", "sawmill", "INSUFFICIENT_RESOURCE")
            .Event(1, 20, "command.place-building", "woodcutter", "accepted")
            .Build();

        FrictionRow row = Assert.Single(Friction.Build(ReportContext.Create(data, _clock, 3, 5)).LastSessionRefusals);

        Assert.Equal(("command.place-building", "INSUFFICIENT_RESOURCE", 1), (row.EventType, row.Outcome, row.Players));
    }
}
