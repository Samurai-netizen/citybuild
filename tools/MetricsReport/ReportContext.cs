namespace CityBuilder.MetricsReport;

/// <summary>
/// What every section of the report shares (docs/plan/CR-0009-T2-PLAN.md §3.3): the population's timelines, "now", who left, and the
/// least sample. The data is already the population: the loader applies the period and <c>--exclude</c>.
/// </summary>
public sealed class ReportContext
{
    /// <summary>Makes the context.</summary>
    /// <param name="timelines">The players' timelines.</param>
    /// <param name="now">The report's "now".</param>
    /// <param name="leftAfterDays">The days unseen after which a player counts as left.</param>
    /// <param name="minSample">The least sample.</param>
    private ReportContext(IReadOnlyList<PlayerTimeline> timelines, DateTimeOffset now, int leftAfterDays, int minSample)
    {
        Timelines = timelines;
        Now = now;
        LeftAfterDays = leftAfterDays;
        MinSample = minSample;
        LeftCount = timelines.Count(t => HasLeft(t.Player));
    }

    /// <summary>One timeline per player of the population, in <c>(created_at, id)</c> order.</summary>
    public IReadOnlyList<PlayerTimeline> Timelines { get; }

    /// <summary>The later of the machine's clock and the newest last seen (a Development server with a fast clock runs ahead).</summary>
    public DateTimeOffset Now { get; }

    /// <summary>The days unseen after which a player counts as left.</summary>
    public int LeftAfterDays { get; }

    /// <summary>A percentage over fewer players than this is marked as a small sample.</summary>
    public int MinSample { get; }

    /// <summary>The players of the population.</summary>
    public int Population => Timelines.Count;

    /// <summary>The players who left.</summary>
    public int LeftCount { get; }

    /// <summary>Makes the context of a data set.</summary>
    /// <param name="data">The population's players and events.</param>
    /// <param name="clock">The machine's clock.</param>
    /// <param name="leftAfterDays">The days unseen after which a player counts as left.</param>
    /// <param name="minSample">The least sample.</param>
    /// <returns>The context.</returns>
    public static ReportContext Create(MetricsData data, DateTimeOffset clock, int leftAfterDays, int minSample)
    {
        DateTimeOffset now = data.Players.Count == 0
            ? clock
            : new[] { clock, DateTimeOffset.FromUnixTimeSeconds(data.Players.Max(p => p.LastSeenUnix)) }.Max();
        return new ReportContext(PlayerTimeline.Build(data), now, leftAfterDays, minSample);
    }

    /// <summary>Whether a player left: not seen for at least the given days before "now".</summary>
    /// <param name="player">The player.</param>
    /// <returns><see langword="true"/> when the player left.</returns>
    public bool HasLeft(MetricsPlayer player) => Now.ToUnixTimeSeconds() - player.LastSeenUnix >= LeftAfterDays * 86_400L;

    /// <summary>A share with this report's least sample.</summary>
    /// <param name="part">The part.</param>
    /// <param name="whole">The whole.</param>
    /// <returns>The percentage.</returns>
    public Rate Percent(long part, long whole) => Rate.Of(part, whole, MinSample);
}
