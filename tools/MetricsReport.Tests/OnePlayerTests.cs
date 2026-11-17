namespace CityBuilder.MetricsReport.Tests;

/// <summary>The one-player section (docs/plan/CR-0009-T2-PLAN.md §3.4): the timeline in order, at most 5000 rows, none for an unknown id.</summary>
public sealed class OnePlayerTests
{
    /// <summary>The context of a data set.</summary>
    /// <param name="data">The data set.</param>
    /// <returns>The context.</returns>
    private static ReportContext Context(TestData data) => ReportContext.Create(data.Build(), TestData.Start.AddDays(10), 3, 5);

    /// <summary>The rows are the player's events in <c>(created_at, id)</c> order, only theirs.</summary>
    [Fact]
    public void Rows_InTimelineOrder()
    {
        TestData data = new TestData()
            .Player(1).Player(2)
            .Event(1, 30, "research.completed", "basic_tools", id: 5)
            .Event(1, 10, "session.start", id: 7)
            .Event(2, 20, "session.start", id: 8)
            .Event(1, 30, "building.completed", "woodcutter", value: 2, id: 4);

        OnePlayerTimeline? timeline = OnePlayer.Build(Context(data), TestData.PlayerId(1));

        Assert.NotNull(timeline);
        Assert.Equal(TestData.PlayerId(1), timeline.PlayerId);
        Assert.Equal([7L, 4L, 5L], timeline.Rows.Select(e => e.Id));
        Assert.Equal(3, timeline.TotalEvents);
    }

    /// <summary>At most 5000 rows: the first ones; the total says how many there are.</summary>
    [Fact]
    public void Rows_AtMost5000()
    {
        var data = new TestData().Player(1);
        for (int second = 0; second <= OnePlayer.MaxRows; second++)
        {
            data.Event(1, second, "session.start");
        }

        OnePlayerTimeline? timeline = OnePlayer.Build(Context(data), TestData.PlayerId(1));

        Assert.NotNull(timeline);
        Assert.Equal(5000, OnePlayer.MaxRows);
        Assert.Equal(OnePlayer.MaxRows, timeline.Rows.Count);
        Assert.Equal(OnePlayer.MaxRows + 1, timeline.TotalEvents);
        Assert.Equal(TestData.Start, timeline.Rows[0].CreatedAt);
    }

    /// <summary>An id not in the population gives no timeline.</summary>
    [Fact]
    public void UnknownId_GivesNone() =>
        Assert.Null(OnePlayer.Build(Context(new TestData().Player(1)), TestData.PlayerId(9)));
}
