using CityBuilder.BalanceSim.Strategies;

namespace CityBuilder.BalanceSim.Tests;

/// <summary>The command line: options, errors, the files it writes, and that the same arguments write the same bytes.</summary>
public sealed class CommandLineTests
{
    /// <summary>The same arguments write byte-identical files: the simulator is deterministic (the task's acceptance rule).</summary>
    [Fact]
    public void SameArguments_WriteTheSameFiles()
    {
        using var first = new TemporaryFolder();
        using var second = new TemporaryFolder();
        string[] args = ["--hours", "24", "--sessions", "every 4h", "--sessions", "08:00,13:00,20:00 daily"];

        Assert.Equal(CommandLine.Success, CommandLine.Run([.. args, "--out", first.Path], TextWriter.Null, TextWriter.Null));
        Assert.Equal(CommandLine.Success, CommandLine.Run([.. args, "--out", second.Path], TextWriter.Null, TextWriter.Null));

        string[] files = [.. Directory.GetFiles(first.Path, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(first.Path, path)).Order(StringComparer.Ordinal)];
        Assert.Equal(1 + (3 * 2 * 2), files.Length);
        Assert.Equal(files, Directory.GetFiles(second.Path, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(second.Path, path)).Order(StringComparer.Ordinal));
        Assert.All(files, file => Assert.Equal(File.ReadAllBytes(Path.Combine(first.Path, file)), File.ReadAllBytes(Path.Combine(second.Path, file))));
    }

    /// <summary>A run's folder holds a summary with every section and a CSV whose lines all have the header's column count.</summary>
    [Fact]
    public void Run_WritesSummaryTimelineAndIndex()
    {
        using var folder = new TemporaryFolder();

        int exit = CommandLine.Run(["--strategy", "greedycoins", "--sessions", "every 4h", "--hours", "12", "--out", folder.Path], TextWriter.Null, TextWriter.Null);

        Assert.Equal(CommandLine.Success, exit);
        string run = Path.Combine(folder.Path, "GreedyCoins-every-4h");
        string summary = File.ReadAllText(Path.Combine(run, "summary.md"));
        foreach (string section in new[] { "## Milestones", "## Coins", "## Goods", "## Workers", "## Producer time by state", "## Logistics", "## City at the end", "## Rejected actions" })
        {
            Assert.Contains(section, summary, StringComparison.Ordinal);
        }

        string[] lines = File.ReadAllLines(Path.Combine(run, "timeline.csv"));
        int columns = lines[0].Split(',').Length;
        Assert.All(lines, line => Assert.Equal(columns, line.Split(',').Length));
        Assert.Contains("GreedyCoins-every-4h/summary.md", File.ReadAllText(Path.Combine(folder.Path, "index.md")), StringComparison.Ordinal);
    }

    /// <summary>Without options every strategy runs with every default pattern for 72 h on the real content.</summary>
    [Fact]
    public void Parse_Defaults_AreEveryStrategyAndPatternFor72Hours()
    {
        CommandLine.Settings settings = CommandLine.Parse([], TestContent.Root);

        Assert.Equal(Enum.GetValues<StrategyKind>().Length * CommandLine.DefaultPatterns.Count, settings.Runs.Count);
        Assert.All(settings.Runs, run => Assert.Equal(72 * 3_600, run.RunSeconds));
        Assert.Equal(Path.Combine(TestContent.Root, "server", "content", "mvp-content.json"), settings.ContentPath);
    }

    /// <summary>An unknown option, a missing value, a bad number or an unknown strategy is a usage error (exit 2).</summary>
    /// <param name="args">The bad command line, joined by "|".</param>
    [Theory]
    [InlineData("--bogus|1")]
    [InlineData("--hours")]
    [InlineData("--hours|0")]
    [InlineData("--hours|x")]
    [InlineData("--strategy|Lazy")]
    [InlineData("--sessions|twice a day")]
    public void BadArguments_AreUsageErrors(string args)
    {
        using var error = new StringWriter();

        int exit = CommandLine.Run(args.Split('|'), TextWriter.Null, error);

        Assert.Equal(CommandLine.Usage, exit);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A content file that can't be loaded is exit 1 with the loader's message.</summary>
    [Fact]
    public void MissingContent_IsAContentError()
    {
        using var folder = new TemporaryFolder();
        using var error = new StringWriter();

        int exit = CommandLine.Run(["--content", Path.Combine(folder.Path, "none.json"), "--out", folder.Path], TextWriter.Null, error);

        Assert.Equal(CommandLine.ContentError, exit);
        Assert.Contains("none.json", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary><c>--help</c> prints the usage and succeeds.</summary>
    [Fact]
    public void Help_PrintsUsage()
    {
        using var output = new StringWriter();

        Assert.Equal(CommandLine.Success, CommandLine.Run(["--help"], output, TextWriter.Null));
        Assert.Contains("--sessions", output.ToString(), StringComparison.Ordinal);
    }
}
