namespace CityBuilder.MetricsReport.Tests;

/// <summary>The actions section (docs/plan/CR-0009-T2-PLAN.md §3.4): per event type the players (count, %), the times, the median per player.</summary>
public sealed class ActionsTests
{
    /// <summary>The actions of a data set with three players, one of them without events.</summary>
    /// <returns>The rows.</returns>
    private static IReadOnlyList<ActionRow> Rows() => Actions.Build(ReportContext.Create(
        new TestData()
            .Player(1).Player(2).Player(3)
            .Event(1, 10, "session.start")
            .Event(1, 20, "command.place-building", "woodcutter", "accepted")
            .Event(1, 30, "command.place-building", "sawmill", "INSUFFICIENT_RESOURCE")
            .Event(1, 40, "command.place-building", "woodcutter", "accepted")
            .Event(1, 50, "session.start")
            .Event(2, 10, "session.start")
            .Event(2, 20, "research.completed", "basic_tools")
            .Build(),
        TestData.Start.AddDays(10), 3, 5));

    /// <summary>One row per event type (details and outcomes together), the most players first, a tie by type.</summary>
    [Fact]
    public void Rows_OnePerEventType_MostPlayersFirst()
    {
        Assert.Equal(["session.start", "command.place-building", "research.completed"], Rows().Select(r => r.EventType));
    }

    /// <summary>Players, their share, the times in total and the median per player who did it.</summary>
    [Fact]
    public void Rows_GivePlayersTimesAndMedian()
    {
        IReadOnlyList<ActionRow> rows = Rows();

        Assert.Equal([2, 1, 1], rows.Select(r => r.Players));
        Assert.Equal(["66.7", "33.3", "33.3"], rows.Select(r => r.Share.ToString()));
        Assert.Equal([3L, 3L, 1L], rows.Select(r => r.Times));
        Assert.Equal([1L, 3L, 1L], rows.Select(r => r.MedianPerPlayer));
    }

    /// <summary>No events: no rows.</summary>
    [Fact]
    public void NoEvents_GiveNoRows() =>
        Assert.Empty(Actions.Build(ReportContext.Create(new TestData().Player(1).Build(), TestData.Start, 3, 5)));
}
