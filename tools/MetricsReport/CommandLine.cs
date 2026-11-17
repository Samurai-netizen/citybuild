namespace CityBuilder.MetricsReport;

/// <summary>The command line of the metrics report (docs/plan/CR-0009-T2-PLAN.md §3.5).</summary>
public static class CommandLine
{
    /// <summary>The exit code when the page was written (or the usage printed).</summary>
    public const int Success = 0;

    /// <summary>The exit code of a bad argument or a bad input file; nothing was written.</summary>
    public const int BadInput = 1;

    /// <summary>The exit code when the database could not be read; nothing was written.</summary>
    public const int DatabaseError = 2;

    /// <summary>What the command line asked for.</summary>
    /// <param name="Help">Whether only the usage is wanted.</param>
    /// <param name="OutPath">The page to write.</param>
    /// <param name="CheckpointsPath">The checkpoint file.</param>
    /// <param name="ContentPath">The content file the checkpoints' ids are checked against.</param>
    /// <param name="From">The first creation day of the population, or none.</param>
    /// <param name="To">The last creation day of the population, or none.</param>
    /// <param name="Exclude">The dev names whose players don't count.</param>
    /// <param name="LeftAfterDays">The days without a visit after which a player counts as left.</param>
    /// <param name="MinSample">The smallest denominator that is not a small sample.</param>
    /// <param name="Player">The player whose timeline is added, or none.</param>
    public sealed record Settings(
        bool Help, string OutPath, string CheckpointsPath, string ContentPath, DateOnly? From, DateOnly? To,
        IReadOnlyList<string> Exclude, int LeftAfterDays, int MinSample, Guid? Player);

    /// <summary>Runs the tool.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="output">Where the written path goes.</param>
    /// <param name="error">Where errors and the usage go.</param>
    /// <returns>An exit code.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error) => throw new NotImplementedException();

    /// <summary>Reads the command line.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="root">The repository root.</param>
    /// <returns>The settings.</returns>
    public static Settings Parse(IReadOnlyList<string> args, string root) => throw new NotImplementedException();
}
