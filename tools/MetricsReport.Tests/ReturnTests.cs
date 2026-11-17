namespace CityBuilder.MetricsReport.Tests;

/// <summary>
/// The return section (docs/plan/CR-0009-T2-PLAN.md §3.4): per creation day (UTC) the players and the share with a <c>session.start</c>
/// on day 1, 3 and 7 after it; "—" while that day has not ended.
/// </summary>
public sealed class ReturnTests
{
    /// <summary>Seconds in a day.</summary>
    private const long _day = 86_400;

    /// <summary>
    /// Three players created on day 0 and one on day 5; the report runs on day 8 at 01:00. Player 1 came back on days 1 and 3, player 2
    /// on days 1, 2 and 7, player 3 never; player 4 on its day 1.
    /// </summary>
    /// <returns>The rows.</returns>
    private static IReadOnlyList<ReturnRow> Rows() => Returns.Build(ReportContext.Create(
        new TestData()
            .Player(1, createdAtSecond: 36_000)
            .Player(2, createdAtSecond: 50)
            .Player(3, createdAtSecond: 80_000)
            .Player(4, createdAtSecond: 5 * _day)
            .Event(1, 36_010, "session.start")
            .Event(1, _day + 100, "session.start")
            .Event(1, (3 * _day) + 5, "session.start")
            .Event(2, 60, "session.start")
            .Event(2, _day + 1, "session.start")
            .Event(2, (2 * _day) + 1, "session.start")
            .Event(2, (7 * _day) + 1, "session.start")
            .Event(2, (7 * _day) + 2, "command.place-building", "woodcutter", "accepted")
            .Event(4, (6 * _day) + 10, "session.start")
            .Build(),
        TestData.Start.AddDays(8).AddHours(1), 3, 5));

    /// <summary>One row per creation day, oldest first, with its players.</summary>
    [Fact]
    public void Rows_OnePerCreationDay()
    {
        IReadOnlyList<ReturnRow> rows = Rows();

        Assert.Equal([new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 6)], rows.Select(r => r.Day));
        Assert.Equal([3, 1], rows.Select(r => r.Players));
        Assert.Equal([1, 3, 7], Returns.Days);
    }

    /// <summary>The share with a session start on exactly day 1, 3 and 7 after the creation day.</summary>
    [Fact]
    public void Returned_OnDays1_3_7()
    {
        Assert.Equal(["66.7", "33.3", "33.3"], Rows()[0].Returned.Select(r => r.ToString()));
    }

    /// <summary>A day that has not ended yet prints "—".</summary>
    [Fact]
    public void Returned_CohortTooYoung_IsDash()
    {
        Assert.Equal(["100.0", Rate.None, Rate.None], Rows()[1].Returned.Select(r => r.ToString()));
    }
}
