namespace CityBuilder.MetricsReport.Tests;

/// <summary>The command line of the report (docs/plan/CR-0009-T2-PLAN.md §3.5): the defaults, every option, and every refusal.</summary>
public sealed class CommandLineTests
{
    /// <summary>A stand-in repository root; parsing only joins paths to it.</summary>
    private static readonly string _root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "metrics-root"));

    /// <summary>With only <c>--out</c>, the files are the repository's, nobody is excluded, and the numbers are the defaults.</summary>
    [Fact]
    public void Parse_OnlyOut_UsesTheDefaults()
    {
        CommandLine.Settings settings = CommandLine.Parse(["--out", "report.html"], _root);

        Assert.False(settings.Help);
        Assert.Equal(Path.Combine(_root, "report.html"), settings.OutPath);
        Assert.Equal(Path.Combine(_root, "server", "analytics", "checkpoints.json"), settings.CheckpointsPath);
        Assert.Equal(Path.Combine(_root, "server", "content", "mvp-content.json"), settings.ContentPath);
        Assert.Null(settings.From);
        Assert.Null(settings.To);
        Assert.Empty(settings.Exclude);
        Assert.Equal(3, settings.LeftAfterDays);
        Assert.Equal(5, settings.MinSample);
        Assert.Null(settings.Player);
    }

    /// <summary>Every option is read; <c>--exclude</c> splits on commas and trims.</summary>
    [Fact]
    public void Parse_EveryOption_IsRead()
    {
        var player = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        CommandLine.Settings settings = CommandLine.Parse(
            ["--out", "/a/r.html", "--checkpoints", "c.json", "--content", "/b/content.json", "--from", "2026-11-01", "--to", "2026-11-30",
             "--exclude", " devtest , qa1,", "--left-after-days", "7", "--min-sample", "2", "--player", player.ToString()], _root);

        Assert.Equal(Path.GetFullPath("/a/r.html"), settings.OutPath);
        Assert.Equal(Path.Combine(_root, "c.json"), settings.CheckpointsPath);
        Assert.Equal(Path.GetFullPath("/b/content.json"), settings.ContentPath);
        Assert.Equal(new DateOnly(2026, 11, 1), settings.From);
        Assert.Equal(new DateOnly(2026, 11, 30), settings.To);
        Assert.Equal(["devtest", "qa1"], settings.Exclude);
        Assert.Equal(7, settings.LeftAfterDays);
        Assert.Equal(2, settings.MinSample);
        Assert.Equal(player, settings.Player);
    }

    /// <summary><c>--help</c> needs no other option.</summary>
    [Fact]
    public void Parse_Help_IsHelp() => Assert.True(CommandLine.Parse(["--help"], _root).Help);

    /// <summary>Each bad command line is refused with a message that names the problem.</summary>
    /// <param name="args">The command line, joined by spaces.</param>
    /// <param name="message">A part of the expected message.</param>
    [Theory]
    [InlineData("", "--out")]
    [InlineData("--out r.html --colour red", "Unknown option --colour")]
    [InlineData("--out", "--out needs a value")]
    [InlineData("--out r.html --from 2026-13-01", "--from")]
    [InlineData("--out r.html --from 2026-11-02 --to 2026-11-01", "--to")]
    [InlineData("--out r.html --left-after-days 0", "--left-after-days")]
    [InlineData("--out r.html --min-sample -1", "--min-sample")]
    [InlineData("--out r.html --min-sample x", "--min-sample")]
    [InlineData("--out r.html --player 42", "--player")]
    public void Parse_BadCommandLine_IsRefused(string args, string message)
    {
        FormatException exception = Assert.Throws<FormatException>(
            () => CommandLine.Parse(args.Split(' ', StringSplitOptions.RemoveEmptyEntries), _root));
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A bad command line ends with exit code 1, the message and the usage on stderr, nothing on stdout, and no file.</summary>
    [Fact]
    public void Run_BadCommandLine_ExitsWithOneAndWritesNothing()
    {
        string outPath = Path.Combine(Path.GetTempPath(), $"metrics-{Guid.NewGuid():N}.html");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = CommandLine.Run(["--out", outPath, "--min-sample", "zero"], output, error);

        Assert.Equal(CommandLine.BadInput, code);
        Assert.Contains("--min-sample", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
        Assert.False(File.Exists(outPath));
    }

    /// <summary><c>--help</c> prints the usage on stdout with exit code 0.</summary>
    [Fact]
    public void Run_Help_PrintsUsage()
    {
        using var output = new StringWriter();

        Assert.Equal(CommandLine.Success, CommandLine.Run(["--help"], output, TextWriter.Null));
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
    }
}
