namespace CityBuilder.Repository.Tests.Architecture;

/// <summary>
/// Guards two rules about the project files themselves: <c>CityBuilder.Api</c> is the only production project that uses ASP.NET Core
/// hosting (<c>core-instructions/03-architecture.md</c>, dependency table), and every production project under <c>server/src</c> is checked
/// for banned APIs (<c>core-instructions/14-coding-standards.md</c>). The assembly-reference tests see only what the code uses; these read
/// what the project files ask for.
/// </summary>
public sealed class ProjectFileTests
{
    /// <summary>The one production project that may use ASP.NET Core, relative to the repository root.</summary>
    private const string _apiProject = "server/src/CityBuilder.Api/CityBuilder.Api.csproj";

    /// <summary>The folders whose project files are production code, relative to the repository root.</summary>
    private static readonly string[] _productionFolders = ["server/src", "shared", "tools"];

    /// <summary>The symbols the task M0-65 bans in production code, as documentation-comment IDs.</summary>
    private static readonly string[] _bannedSymbols =
    [
        "P:System.DateTime.Now", "P:System.DateTime.UtcNow", "P:System.DateTimeOffset.Now", "P:System.DateTimeOffset.UtcNow",
        "M:System.Threading.Thread.Sleep(System.Int32)", "M:System.Threading.Thread.Sleep(System.TimeSpan)",
    ];

    /// <summary>No production project file but the Api's uses the Web SDK, the ASP.NET Core framework or an ASP.NET Core package.</summary>
    [Fact]
    public void ProductionProjectFiles_ExceptApi_DoNotUseAspNetCore()
    {
        string root = RepositoryPaths.Root;

        string[] offenders = ProductionProjects(root)
            .Where(path => path != _apiProject)
            .Where(path => UsesAspNetCore(File.ReadAllText(Path.Combine(root, path))))
            .ToArray();

        Assert.True(offenders.Length == 0, "Only the Api project may use ASP.NET Core, but these do:\n" + string.Join("\n", offenders));
    }

    /// <summary>The check above sees the Api project as an ASP.NET Core user and finds the production projects; it can't pass on an empty list.</summary>
    [Fact]
    public void ProductionProjects_IncludeTheApiAndTheOtherServerProjects()
    {
        string root = RepositoryPaths.Root;
        string[] projects = ProductionProjects(root).ToArray();

        Assert.Contains(_apiProject, projects);
        Assert.Contains("server/src/CityBuilder.Infrastructure/CityBuilder.Infrastructure.csproj", projects);
        Assert.Contains("shared/Simulation.Build/CityBuilder.Simulation.csproj", projects);
        Assert.True(UsesAspNetCore(File.ReadAllText(Path.Combine(root, _apiProject))));
    }

    /// <summary><c>server/Directory.Build.props</c> gives every <c>src/</c> project the banned-API analyzer and the symbol list.</summary>
    [Fact]
    public void DirectoryBuildProps_GivesEverySrcProjectTheBannedApiAnalyzer()
    {
        string props = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "server/Directory.Build.props"));

        Assert.Contains("Microsoft.CodeAnalysis.BannedApiAnalyzers", props);
        Assert.Contains("BannedSymbols.txt", props);
        Assert.Contains("StartsWith('src')", props);
    }

    /// <summary><c>server/BannedSymbols.txt</c> bans the clock properties and <c>Thread.Sleep</c>, each with a message.</summary>
    [Fact]
    public void BannedSymbols_BanTheClockAndSleep()
    {
        string[] lines = File.ReadAllLines(Path.Combine(RepositoryPaths.Root, "server/BannedSymbols.txt"));

        foreach (string symbol in _bannedSymbols)
        {
            string? line = lines.SingleOrDefault(candidate => candidate.StartsWith(symbol + ";", StringComparison.Ordinal));
            Assert.True(line is not null, $"{symbol} is not banned in server/BannedSymbols.txt");
            Assert.True(line.Length > symbol.Length + 1, $"{symbol} has no message");
        }
    }

    /// <summary>The project files of production code: <c>server/src</c>, the shared engine's build project and the tools (not their test projects), as paths relative to the root.</summary>
    private static IEnumerable<string> ProductionProjects(string root) =>
        _productionFolders
            .SelectMany(folder => Directory.GetFiles(Path.Combine(root, folder), "*.csproj", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal))
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith(".Tests", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    /// <summary>True when the project text uses the Web SDK, the ASP.NET Core shared framework or any <c>Microsoft.AspNetCore</c> package.</summary>
    private static bool UsesAspNetCore(string projectText) =>
        projectText.Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal)
        || projectText.Contains("Microsoft.AspNetCore", StringComparison.Ordinal);
}
