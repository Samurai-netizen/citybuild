namespace CityBuilder.Repository.Tests.Architecture;

/// <summary>
/// Guards the engine rules of <c>.claude/rules/simulation.md</c> and <c>core-instructions/02-principles.md</c> (rules 6 and 8) over every
/// source file of <c>shared/Simulation/Runtime/</c>, and shows that the scans in <see cref="EngineSourceScan"/> do catch a violation.
/// </summary>
public sealed class EngineSourceTests
{
    /// <summary>The engine's sources, found once; the tests below fail when the folder is empty because a moved folder would otherwise pass them all.</summary>
    private static readonly IReadOnlyList<string> _files = EngineSourceScan.Files(RepositoryPaths.Root);

    /// <summary>The scans see the engine's files; an empty list would make every scan pass.</summary>
    [Fact]
    public void Files_OfTheEngine_AreFound()
    {
        Assert.Contains("shared/Simulation/Runtime/IntegerMath.cs", _files);
        Assert.True(_files.Count > 20, $"only {_files.Count} engine files were found");
    }

    /// <summary>No engine file uses <c>float</c>, <c>double</c> or <c>decimal</c> (integers only).</summary>
    [Fact]
    public void FloatingPointTypes_InEngineSources_AreAbsent() =>
        AssertNone(path => EngineSourceScan.FloatingPointTypes(path, Read(path)));

    /// <summary>No engine file uses a clock, a sleep or randomness.</summary>
    [Fact]
    public void TimeAndRandomness_InEngineSources_AreAbsent() =>
        AssertNone(path => EngineSourceScan.TimeAndRandomness(path, Read(path)));

    /// <summary>No engine file holds a content id as a string or compares an era with a number.</summary>
    [Fact]
    public void ContentLiterals_InEngineSources_AreAbsent()
    {
        IReadOnlySet<string> ids = EngineSourceScan.ContentIds(RepositoryPaths.Root);
        Assert.Contains("sharp_axes", ids);
        AssertNone(path => EngineSourceScan.ContentLiterals(path, Read(path), ids));
    }

    /// <summary>The floating-point scan reports a field, and ignores the same words in a comment or a string.</summary>
    [Fact]
    public void FloatingPointTypes_OfSeededSource_ReportOnlyTheCode()
    {
        const string Source = "class A { double _x; /* float */ string s = \"decimal\"; decimal Y() => 1m; }";

        Assert.Equal(2, EngineSourceScan.FloatingPointTypes("A.cs", Source).Count);
    }

    /// <summary>The clock scan reports <c>DateTime.UtcNow</c> and <c>Thread.Sleep</c>.</summary>
    [Fact]
    public void TimeAndRandomness_OfSeededSource_ReportClockAndSleep()
    {
        const string Source = "class A { void M() { var t = DateTime.UtcNow; Thread.Sleep(1); } }";

        Assert.Equal(2, EngineSourceScan.TimeAndRandomness("A.cs", Source).Count);
    }

    /// <summary>The content scan reports a research id and an era comparison, and not a number that isn't compared with an era, nor an era bound of 1.</summary>
    [Fact]
    public void ContentLiterals_OfSeededSource_ReportIdAndEraNumber()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { "sharp_axes" };
        const string Source = "class A { bool M(int era, int n) => era == 2 || city.Era >= 3 || n == 2 || era < 1 || id == \"sharp_axes\"; }";

        Assert.Equal(3, EngineSourceScan.ContentLiterals("A.cs", Source, ids).Count);
    }

    /// <summary>Asserts that a scan reports nothing for any engine file; the message lists every report.</summary>
    private static void AssertNone(Func<string, IReadOnlyList<string>> scan)
    {
        string[] problems = _files.SelectMany(scan).ToArray();

        Assert.True(problems.Length == 0, $"{problems.Length} problem(s) in the engine's sources:\n" + string.Join("\n", problems));
    }

    /// <summary>The text of one engine file, given its path relative to the repository root.</summary>
    private static string Read(string path) => File.ReadAllText(Path.Combine(RepositoryPaths.Root, path));
}
