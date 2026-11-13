using System.Xml.Linq;

namespace CityBuilder.Repository.Tests.Architecture;

/// <summary>
/// Guards three rules about the project files themselves: <c>CityBuilder.Api</c> is the only production project that uses ASP.NET Core
/// hosting (<c>core-instructions/03-architecture.md</c>, dependency table), each <c>server/src</c> project references only the projects that
/// table allows (M0-66), and every production project under <c>server/src</c> is checked for banned APIs
/// (<c>core-instructions/14-coding-standards.md</c>). The assembly-reference tests see only what the code uses; these read
/// what the project files ask for.
/// </summary>
public sealed class ProjectFileTests
{
    /// <summary>The one production project that may use ASP.NET Core, relative to the repository root.</summary>
    private const string _apiProject = "server/src/CityBuilder.Api/CityBuilder.Api.csproj";

    /// <summary>The folders whose project files are production code, relative to the repository root.</summary>
    private static readonly string[] _productionFolders = ["server/src", "shared", "tools"];

    /// <summary>
    /// The "May reference" column of the dependency table in <c>core-instructions/03-architecture.md</c>, for the projects under
    /// <c>server/src</c>: each project's name and the project names it may reference (by file name, without <c>.csproj</c>).
    /// </summary>
    public static readonly TheoryData<string, string[]> AllowedServerReferences = new()
    {
        { "CityBuilder.Domain", ["CityBuilder.Simulation"] },
        { "CityBuilder.Application", ["CityBuilder.Domain", "CityBuilder.Simulation"] },
        { "CityBuilder.Infrastructure", ["CityBuilder.Application", "CityBuilder.Domain", "CityBuilder.Simulation"] },
        { "CityBuilder.Api", ["CityBuilder.Application", "CityBuilder.Infrastructure"] },
    };

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

    /// <summary>A <c>server/src</c> project's project references are all in its "May reference" cell of the dependency table.</summary>
    /// <param name="project">The project's name.</param>
    /// <param name="allowed">The project names it may reference.</param>
    [Theory]
    [MemberData(nameof(AllowedServerReferences))]
    public void ServerProjectReferences_StayInsideTheDependencyTable(string project, string[] allowed)
    {
        string[] references = ProjectReferences(Path.Combine(RepositoryPaths.Root, "server/src", project, project + ".csproj"));

        string[] extra = references.Except(allowed, StringComparer.Ordinal).ToArray();
        Assert.True(extra.Length == 0, $"{project} references {string.Join(", ", extra)}, which core-instructions/03 does not allow.");
    }

    /// <summary>The table above names every <c>server/src</c> project, and the reader sees real references (the Api's two), so the check can't pass on nothing.</summary>
    [Fact]
    public void AllowedServerReferences_CoverEverySrcProject()
    {
        string root = RepositoryPaths.Root;
        string[] projects = Directory.GetFiles(Path.Combine(root, "server/src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] named = AllowedServerReferences.Select(row => row.Data.Item1).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(named, projects);
        Assert.Equal(["CityBuilder.Application", "CityBuilder.Infrastructure"], ProjectReferences(Path.Combine(root, _apiProject)));
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

    /// <summary>The project names a project file references with <c>ProjectReference</c>, by file name without <c>.csproj</c>, in ordinal order.</summary>
    private static string[] ProjectReferences(string projectFile) =>
        XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(((string?)reference.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>True when the project text uses the Web SDK, the ASP.NET Core shared framework or any <c>Microsoft.AspNetCore</c> package.</summary>
    private static bool UsesAspNetCore(string projectText) =>
        projectText.Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal)
        || projectText.Contains("Microsoft.AspNetCore", StringComparison.Ordinal);
}
