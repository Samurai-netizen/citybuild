using System.Globalization;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    /// <summary>What the tool prints for <c>--help</c> or a bad command line.</summary>
    private const string _usageText = """
        Usage: dotnet run --project tools/MetricsReport -- --out <file> [options]
          --out <file>              the HTML page to write (required)
          --checkpoints <file>      the checkpoint file (default: server/analytics/checkpoints.json)
          --content <file>          the content file the checkpoints' ids are checked against (default: server/content/mvp-content.json)
          --from <yyyy-mm-dd>       only players created on or after this day (UTC)
          --to <yyyy-mm-dd>         only players created on or before this day (UTC)
          --exclude <names>         dev names whose players don't count, comma-separated (for example devtest)
          --left-after-days <n>     days without a visit after which a player counts as left (default: 3)
          --min-sample <n>          a percentage over fewer players is marked as a small sample (default: 5)
          --player <id>             add this player's timeline
          --help                    this text
        The database is the one of the environment variable ConnectionStrings__CityBuilder; the tool only reads it.
        Relative paths are resolved against the repository root (the first folder above the tool with global.json).
        """;

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

    /// <summary>The environment variable that holds the connection string (the API's key; never an argument: the process list shows those).</summary>
    public const string ConnectionVariable = "ConnectionStrings__CityBuilder";

    /// <summary>Runs the tool on the database of <see cref="ConnectionVariable"/>.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="output">Where the written path goes.</param>
    /// <param name="error">Where errors and the usage go.</param>
    /// <returns>An exit code.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error) =>
        Run(args, output, error, Environment.GetEnvironmentVariable(ConnectionVariable));

    /// <summary>Runs the tool on a database.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="output">Where the written path goes.</param>
    /// <param name="error">Where errors and the usage go.</param>
    /// <param name="connectionString">The database's connection string, or none.</param>
    /// <returns>An exit code.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        Settings settings;
        try
        {
            settings = Parse(args, RepositoryRoot.Find(AppContext.BaseDirectory));
        }
        catch (FormatException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine(_usageText);
            return BadInput;
        }

        if (settings.Help)
        {
            output.WriteLine(_usageText);
            return Success;
        }

        CheckpointFile checkpoints;
        try
        {
            checkpoints = CheckpointFile.Load(settings.CheckpointsPath, ContentFileLoader.Load(settings.ContentPath));
        }
        catch (Exception exception) when (exception is CheckpointFileException or ContentLoadException)
        {
            error.WriteLine(exception.Message);
            return BadInput;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            error.WriteLine($"MetricsReport: set the environment variable {ConnectionVariable} to the database's connection string.");
            return DatabaseError;
        }

        MetricsData data;
        try
        {
            data = Load(connectionString, new LoadFilter(settings.From, settings.To, settings.Exclude));
        }
        catch (Exception exception) when (exception is MetricsDatabaseException or NpgsqlException or ArgumentException)
        {
            error.WriteLine("MetricsReport: the database can't be read: " + exception.Message);
            return DatabaseError;
        }

        _ = (checkpoints, data);
        error.WriteLine("MetricsReport: the page is not built yet (CR-0009-T2 in progress).");
        return BadInput;
    }

    /// <summary>Loads the data from a database.</summary>
    /// <param name="connectionString">The database's connection string.</param>
    /// <param name="filter">The period and the excluded dev names.</param>
    /// <returns>The data.</returns>
    private static MetricsData Load(string connectionString, LoadFilter filter)
    {
        using var db = new CityBuilderDbContext(new DbContextOptionsBuilder<CityBuilderDbContext>().UseNpgsql(connectionString).Options);
        return MetricsLoader.LoadAsync(db, filter, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Reads the command line.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="root">The repository root.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="FormatException">An option is unknown, has no value or a bad one, or <c>--out</c> is missing.</exception>
    public static Settings Parse(IReadOnlyList<string> args, string root)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? outPath = null;
        string checkpoints = "server/analytics/checkpoints.json";
        string content = "server/content/mvp-content.json";
        DateOnly? from = null, to = null;
        string[] exclude = [];
        int leftAfterDays = 3, minSample = 5;
        Guid? player = null;
        for (int index = 0; index < args.Count; index++)
        {
            string option = args[index];
            if (option == "--help")
            {
                return new Settings(true, string.Empty, string.Empty, string.Empty, null, null, [], leftAfterDays, minSample, null);
            }

            string value = index + 1 < args.Count ? args[++index] : throw new FormatException($"{option} needs a value.");
            switch (option)
            {
                case "--out":
                    outPath = value;
                    break;
                case "--checkpoints":
                    checkpoints = value;
                    break;
                case "--content":
                    content = value;
                    break;
                case "--from":
                    from = Day(option, value);
                    break;
                case "--to":
                    to = Day(option, value);
                    break;
                case "--exclude":
                    exclude = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "--left-after-days":
                    leftAfterDays = Positive(option, value);
                    break;
                case "--min-sample":
                    minSample = Positive(option, value);
                    break;
                case "--player":
                    player = Guid.TryParse(value, CultureInfo.InvariantCulture, out Guid id)
                        ? id
                        : throw new FormatException($"--player needs a player id (a uuid), not {value}.");
                    break;
                default:
                    throw new FormatException($"Unknown option {option}.");
            }
        }

        if (outPath is null)
        {
            throw new FormatException("--out names the page to write; it is required.");
        }

        if (from is not null && to is not null && to < from)
        {
            throw new FormatException("--to is before --from.");
        }

        return new Settings(
            false, Path.GetFullPath(outPath, root), Path.GetFullPath(checkpoints, root), Path.GetFullPath(content, root), from, to, exclude,
            leftAfterDays, minSample, player);
    }

    /// <summary>Reads a day in <c>yyyy-mm-dd</c> form.</summary>
    /// <param name="option">The option, for the message.</param>
    /// <param name="value">The text.</param>
    /// <returns>The day.</returns>
    /// <exception cref="FormatException">The text is not such a day.</exception>
    private static DateOnly Day(string option, string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day)
            ? day
            : throw new FormatException($"{option} needs a day as yyyy-mm-dd, not {value}.");

    /// <summary>Reads a whole number from 1 to 100 000.</summary>
    /// <param name="option">The option, for the message.</param>
    /// <param name="value">The text.</param>
    /// <returns>The number.</returns>
    /// <exception cref="FormatException">The text is not such a number.</exception>
    private static int Positive(string option, string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number is > 0 and <= 100_000
            ? number
            : throw new FormatException($"{option} needs a whole number from 1 to 100000, not {value}.");
}
