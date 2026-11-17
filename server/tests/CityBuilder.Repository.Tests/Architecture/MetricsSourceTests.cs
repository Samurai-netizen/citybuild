namespace CityBuilder.Repository.Tests.Architecture;

/// <summary>
/// Guards the player events (CR-0009) with <see cref="MetricsSourceScan"/>: on the server only the context, the row, its configuration and
/// the write-only repository name the stored rows; <c>shared/</c> and <c>client/</c> never name a player event. The seeded tests show that
/// the scans catch a violation.
/// </summary>
public sealed class MetricsSourceTests
{
    /// <summary>The server's sources, found once.</summary>
    private static readonly IReadOnlyList<string> _serverFiles = MetricsSourceScan.ServerFiles(RepositoryPaths.Root);

    /// <summary>The engine's and the client's sources, found once.</summary>
    private static readonly IReadOnlyList<string> _sharedAndClientFiles = MetricsSourceScan.SharedAndClientFiles(RepositoryPaths.Root);

    /// <summary>The scans see the files they guard, and every allowed file exists; an empty list would make every scan pass.</summary>
    [Fact]
    public void Files_OfServerSharedAndClient_AreFound()
    {
        Assert.Contains("server/src/CityBuilder.Application/Commands/CommandPipeline.cs", _serverFiles);
        Assert.DoesNotContain(_serverFiles, path => path.Contains("/Migrations/", StringComparison.Ordinal));
        Assert.Contains("shared/Simulation/Runtime/IntegerMath.cs", _sharedAndClientFiles);
        Assert.Contains(_sharedAndClientFiles, path => path.StartsWith("client/", StringComparison.Ordinal));
        Assert.Equal(4, MetricsSourceScan.RowFiles.Count);
        Assert.All(MetricsSourceScan.RowFiles, path => Assert.Contains(path, _serverFiles));
    }

    /// <summary>On the server, only the allowed files name the stored rows: game code reaches events only through the write-only port.</summary>
    [Fact]
    public void RowUses_InServerSources_AreOnlyInTheAllowedFiles() =>
        AssertNone(_serverFiles, path => MetricsSourceScan.RowUses(path, Read(path)));

    /// <summary>No engine or client file names a player event, not even in a comment.</summary>
    [Fact]
    public void Mentions_InSharedAndClientSources_AreAbsent() =>
        AssertNone(_sharedAndClientFiles, path => MetricsSourceScan.Mentions(path, Read(path)));

    /// <summary>The row scan reports the set, the row type and the table name in code, and ignores a comment and an allowed file.</summary>
    [Fact]
    public void RowUses_OfSeededSource_ReportOnlyTheCode()
    {
        const string Source = "class A { // PlayerEvents\n int M(Db db) => db.PlayerEvents.Count(); PlayerEventRow r; string t = \"player_events\"; }";
        string allowed = MetricsSourceScan.RowFiles.First();

        Assert.Equal(3, MetricsSourceScan.RowUses("server/src/A.cs", Source).Count);
        Assert.Empty(MetricsSourceScan.RowUses(allowed, Source));
    }

    /// <summary>The mention scan reports a type and a comment that name an event, once per line, and not an unrelated word.</summary>
    [Fact]
    public void Mentions_OfSeededSource_ReportEachLine()
    {
        const string Source = "class A {\n PlayerEvent e; // a PlayerEvent\n // player_events\n string Player; }";

        Assert.Equal(["A.cs:2: names a player event", "A.cs:3: names a player event"], MetricsSourceScan.Mentions("A.cs", Source));
    }

    /// <summary>Asserts that a scan reports nothing for any of the files; the message lists every report.</summary>
    private static void AssertNone(IReadOnlyList<string> files, Func<string, IReadOnlyList<string>> scan)
    {
        string[] problems = files.SelectMany(scan).ToArray();

        Assert.True(problems.Length == 0, $"{problems.Length} problem(s):\n" + string.Join("\n", problems));
    }

    /// <summary>The text of one file, given its path relative to the repository root.</summary>
    private static string Read(string path) => File.ReadAllText(Path.Combine(RepositoryPaths.Root, path));
}
