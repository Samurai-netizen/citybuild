namespace CityBuilder.MetricsReport;

/// <summary>The report page (docs/plan/CR-0009-T2-PLAN.md §3.4): one self-contained HTML file.</summary>
public static class HtmlReport
{
    /// <summary>Writes the page.</summary>
    /// <param name="context">The population and its numbers' context.</param>
    /// <param name="file">The checkpoint file.</param>
    /// <param name="settings">What the overview says about the run.</param>
    /// <param name="player">The timeline of <c>--player</c>, or none.</param>
    /// <returns>The page.</returns>
    public static string Render(ReportContext context, CheckpointFile file, ReportSettings settings, OnePlayerTimeline? player) =>
        throw new NotImplementedException();
}

/// <summary>What the overview says about the run, apart from what the context holds.</summary>
/// <param name="From">The first creation day of the population, or none.</param>
/// <param name="To">The last creation day of the population, or none.</param>
/// <param name="ExcludedNames">How many dev names were excluded (the names themselves never appear: ids only).</param>
/// <param name="Clock">The machine's clock when the report ran.</param>
public sealed record ReportSettings(DateOnly? From, DateOnly? To, int ExcludedNames, DateTimeOffset Clock);
