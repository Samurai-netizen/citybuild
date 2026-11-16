using System.Globalization;
using CityBuilder.BalanceSim.Output;
using CityBuilder.BalanceSim.Strategies;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Simulation.Content;

namespace CityBuilder.BalanceSim;

/// <summary>
/// The command line of the balance simulator (docs/how-to/run-balance-simulator.md): it reads the options, loads the content, plays every
/// strategy × check-in pattern, and writes one folder per run plus <c>index.md</c>.
/// </summary>
public static class CommandLine
{
    /// <summary>The exit code when every run was written.</summary>
    public const int Success = 0;

    /// <summary>The exit code when the content file can't be loaded.</summary>
    public const int ContentError = 1;

    /// <summary>The exit code of a command line the tool doesn't understand.</summary>
    public const int Usage = 2;

    /// <summary>The check-in patterns when none is given: the three examples of the task and of core-instructions/24-economy-design.md.</summary>
    public static readonly IReadOnlyList<string> DefaultPatterns = ["08:00,13:00,20:00 daily", "every 4h", "once a day"];

    /// <summary>What the tool prints for <c>--help</c> or a bad command line.</summary>
    private const string _usageText = """
        Usage: dotnet run --project tools/BalanceSim -- [options]
          --strategy <list>             GreedyCoins, ResearchRush, Balanced, comma-separated, or all (default: all)
          --sessions <pattern>          a check-in pattern; repeat for several (default: the three below)
                                        "08:00,13:00,20:00 daily" | "every 4h" | "every 90min" | "once a day"
          --hours <n>                   length of each run in hours (default: 72)
          --content <file>              content JSON (default: server/content/mvp-content.json)
          --out <folder>                where the runs are written (default: artifacts/balance-sim)
          --first-session-minutes <n>   length of the first check-in (default: 30)
          --session-minutes <n>         length of every later check-in (default: 5)
          --sample-minutes <n>          spacing of the timeline rows (default: 15)
          --grace-seconds <n>           seconds after a check-in until the player counts as absent (default: 300)
          --help                        this text
        Relative paths are resolved against the repository root (the first folder above the tool with global.json).
        """;

    /// <summary>Runs the tool.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="output">Where progress goes.</param>
    /// <param name="error">Where the usage text and errors go.</param>
    /// <returns><see cref="Success"/>, <see cref="ContentError"/> or <see cref="Usage"/>.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        string root = RepositoryRoot.Find(AppContext.BaseDirectory);
        Settings settings;
        try
        {
            settings = Parse(args, root);
        }
        catch (FormatException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine(_usageText);
            return Usage;
        }

        if (settings.Help)
        {
            output.WriteLine(_usageText);
            return Success;
        }

        GameContent content;
        try
        {
            content = ContentFileLoader.Load(settings.ContentPath);
        }
        catch (ContentLoadException exception)
        {
            error.WriteLine(exception.Message);
            return ContentError;
        }

        IReadOnlyList<RunResult> results = RunWriter.PlayAll(content, settings.Runs);
        RunWriter.Write(settings.OutFolder, results);
        output.WriteLine($"BalanceSim: {results.Count} runs of {content.ContentVersion} written to {settings.OutFolder} (index.md).");
        return Success;
    }

    /// <summary>Reads the command line.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="root">The repository root, for relative paths and the defaults.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="FormatException">An option is unknown, has no value or a bad one.</exception>
    public static Settings Parse(IReadOnlyList<string> args, string root)
    {
        ArgumentNullException.ThrowIfNull(args);
        var strategies = Enum.GetValues<StrategyKind>().ToList();
        var patterns = new List<SessionPattern>();
        long hours = 72, firstSessionMinutes = 30, sessionMinutes = 5, sampleMinutes = 15, graceSeconds = 300;
        string content = "server/content/mvp-content.json";
        string outFolder = "artifacts/balance-sim";
        for (int index = 0; index < args.Count; index++)
        {
            string option = args[index];
            if (option == "--help")
            {
                return new Settings(true, string.Empty, string.Empty, []);
            }

            string value = index + 1 < args.Count ? args[++index] : throw new FormatException($"{option} needs a value.");
            switch (option)
            {
                case "--strategy":
                    strategies = ParseStrategies(value);
                    break;
                case "--sessions":
                    patterns.Add(SessionPattern.Parse(value));
                    break;
                case "--hours":
                    hours = Positive(option, value);
                    break;
                case "--content":
                    content = value;
                    break;
                case "--out":
                    outFolder = value;
                    break;
                case "--first-session-minutes":
                    firstSessionMinutes = Positive(option, value);
                    break;
                case "--session-minutes":
                    sessionMinutes = Positive(option, value);
                    break;
                case "--sample-minutes":
                    sampleMinutes = Positive(option, value);
                    break;
                case "--grace-seconds":
                    graceSeconds = Positive(option, value, allowZero: true);
                    break;
                default:
                    throw new FormatException($"Unknown option {option}.");
            }
        }

        if (patterns.Count == 0)
        {
            patterns.AddRange(DefaultPatterns.Select(SessionPattern.Parse));
        }

        RunOptions[] runs = [.. strategies.SelectMany(strategy => patterns.Select(pattern => new RunOptions(
            strategy, pattern, checked(hours * 3_600), checked(firstSessionMinutes * 60), checked(sessionMinutes * 60),
            checked(sampleMinutes * 60), graceSeconds)))];
        return new Settings(false, Path.GetFullPath(content, root), Path.GetFullPath(outFolder, root), runs);
    }

    /// <summary>Reads a strategy list: names (any case), comma-separated, or <c>all</c>.</summary>
    /// <param name="value">The list.</param>
    /// <returns>The strategies, each once, in the order given.</returns>
    /// <exception cref="FormatException">A name is unknown.</exception>
    private static List<StrategyKind> ParseStrategies(string value)
    {
        if (value.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return [.. Enum.GetValues<StrategyKind>()];
        }

        var kinds = new List<StrategyKind>();
        foreach (string name in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            StrategyKind kind = Enum.GetValues<StrategyKind>().FirstOrDefault(
                candidate => candidate.ToString().Equals(name, StringComparison.OrdinalIgnoreCase), (StrategyKind)(-1));
            if (kind == (StrategyKind)(-1))
            {
                throw new FormatException($"Unknown strategy {name}. Use {string.Join(", ", Enum.GetNames<StrategyKind>())} or all.");
            }

            if (!kinds.Contains(kind))
            {
                kinds.Add(kind);
            }
        }

        return kinds.Count > 0 ? kinds : throw new FormatException("--strategy names no strategy.");
    }

    /// <summary>Reads a whole number above 0 (or at least 0).</summary>
    /// <param name="option">The option, for the message.</param>
    /// <param name="value">The text.</param>
    /// <param name="allowZero">Whether 0 is allowed.</param>
    /// <returns>The number.</returns>
    /// <exception cref="FormatException">The text is not such a number.</exception>
    private static long Positive(string option, string value, bool allowZero = false)
    {
        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number <= 1_000_000
            && (number > 0 || allowZero))
        {
            return number;
        }

        throw new FormatException($"{option} needs a whole number {(allowZero ? "of at least 0" : "above 0")} (at most 1000000), not \"{value}\".");
    }

    /// <summary>The command line, read.</summary>
    /// <param name="Help">Whether <c>--help</c> was asked for (then nothing else is set).</param>
    /// <param name="ContentPath">The content file's full path.</param>
    /// <param name="OutFolder">The output folder's full path.</param>
    /// <param name="Runs">Every strategy × pattern, strategies first.</param>
    public sealed record Settings(bool Help, string ContentPath, string OutFolder, IReadOnlyList<RunOptions> Runs);
}
