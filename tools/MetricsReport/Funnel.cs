namespace CityBuilder.MetricsReport;

/// <summary>An ordered list of checkpoints the page shows as a funnel; optional, a checkpoint may be in several or none (CR-0009 part 2).</summary>
/// <param name="Name">The funnel's name on the page.</param>
/// <param name="Steps">The checkpoint ids in order; each names a checkpoint of the file, none twice.</param>
public sealed record Funnel(string Name, IReadOnlyList<string> Steps);
