namespace CityBuilder.MetricsReport.Tests;

/// <summary>
/// The funnels section (docs/plan/CR-0009-T2-PLAN.md §3.3, §3.4): step k counts the players who reached steps 1 to k, each at or after
/// the step before; the shares of the population and of the step before, the drop, and the median time from the step before.
/// </summary>
public sealed class FunnelsTests
{
    /// <summary>The named checkpoints: a woodcutter placed, a research completed, a second session, a research started (nobody).</summary>
    private static readonly IReadOnlyList<Checkpoint> _named =
    [
        new("placed_woodcutter", "Placed a woodcutter", "command.place-building", "woodcutter", "accepted", null, 1),
        new("research_done", "Completed a research", "research.completed", null, null, null, 1),
        new("came_back", "Came back", "session.start", null, null, null, 2),
        new("started_research", "Started a research", "command.start-research", null, null, null, 1),
    ];

    /// <summary>
    /// Four players. Player 1 reached the three steps in order. Player 2 came back (step 3) before completing a research (step 2).
    /// Player 3 completed a research without a woodcutter. Player 4 did nothing.
    /// </summary>
    /// <returns>The data set.</returns>
    private static MetricsData Data() => new TestData()
        .Player(1).Player(2).Player(3).Player(4)
        .Event(1, 10, "session.start")
        .Event(1, 100, "command.place-building", "woodcutter", "accepted")
        .Event(1, 300, "research.completed", "basic_tools")
        .Event(1, 1_000, "session.start")
        .Event(2, 10, "session.start")
        .Event(2, 50, "command.place-building", "woodcutter", "accepted")
        .Event(2, 500, "session.start")
        .Event(2, 900, "research.completed", "basic_tools")
        .Event(3, 100, "research.completed", "basic_tools")
        .Build();

    /// <summary>The funnel tables of the data with the given funnels.</summary>
    /// <param name="funnels">The funnels of the checkpoint file.</param>
    /// <returns>The tables.</returns>
    private static IReadOnlyList<FunnelTable> Tables(params Funnel[] funnels) =>
        Funnels.Build(ReportContext.Create(Data(), TestData.Start.AddDays(10), 3, 5), new CheckpointFile(_named, funnels));

    /// <summary>Each step counts only the players who reached every step before it, in order.</summary>
    [Fact]
    public void Steps_CountPlayersWhoReachedEveryStepBeforeInOrder()
    {
        FunnelTable table = Assert.Single(Tables(new Funnel("First hour", ["placed_woodcutter", "research_done", "came_back"])));

        Assert.Equal("First hour", table.Name);
        Assert.Equal(["Placed a woodcutter", "Completed a research", "Came back"], table.Steps.Select(s => s.Title));
        Assert.Equal([2, 2, 1], table.Steps.Select(s => s.Reached));
    }

    /// <summary>The shares of the population and of the step before (the population for step 1), and the drop.</summary>
    [Fact]
    public void Steps_GiveSharesAndDrop()
    {
        FunnelTable table = Tables(new Funnel("First hour", ["placed_woodcutter", "research_done", "came_back"]))[0];

        Assert.Equal(["50.0", "50.0", "25.0"], table.Steps.Select(s => s.OfPopulation.ToString()));
        Assert.Equal(["50.0", "100.0", "50.0"], table.Steps.Select(s => s.OfPrevious.ToString()));
        Assert.Equal([2, 0, 1], table.Steps.Select(s => s.Drop));
    }

    /// <summary>The median time from the step before (from the creation for step 1), over the players counted at the step.</summary>
    [Fact]
    public void Steps_GiveMedianSecondsFromThePreviousStep()
    {
        FunnelTable table = Tables(new Funnel("First hour", ["placed_woodcutter", "research_done", "came_back"]))[0];

        Assert.Equal([50L, 200L, 700L], table.Steps.Select(s => s.MedianSecondsFromPrevious));
    }

    /// <summary>A funnel nobody enters: zero players, 0.0 of the population, "—" after the first step, no median.</summary>
    [Fact]
    public void FunnelNobodyEnters_GivesZerosAndDashes()
    {
        FunnelTable table = Tables(new Funnel("Research", ["started_research", "research_done"]))[0];

        Assert.Equal([0, 0], table.Steps.Select(s => s.Reached));
        Assert.Equal(["0.0", "0.0"], table.Steps.Select(s => s.OfPopulation.ToString()));
        Assert.Equal(["0.0", Rate.None], table.Steps.Select(s => s.OfPrevious.ToString()));
        Assert.Equal([4, 0], table.Steps.Select(s => s.Drop));
        Assert.All(table.Steps, s => Assert.Null(s.MedianSecondsFromPrevious));
    }

    /// <summary>The tables follow the file's funnel order; no funnel gives no table.</summary>
    [Fact]
    public void Tables_FollowTheFileOrder()
    {
        Assert.Empty(Tables());
        Assert.Equal(
            ["B", "A"],
            Tables(new Funnel("B", ["came_back"]), new Funnel("A", ["placed_woodcutter"])).Select(t => t.Name));
    }
}
