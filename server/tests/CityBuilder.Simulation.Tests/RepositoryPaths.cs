namespace CityBuilder.Simulation.Tests;

/// <summary>Finds the repository's own files (the Unity package, the build project) from the folder the tests run in.</summary>
internal static class RepositoryPaths
{
    /// <summary>
    /// The repository root. The tests run from <c>server/tests/&lt;project&gt;/bin/&lt;configuration&gt;/&lt;framework&gt;/</c>,
    /// so it is found by walking up to <c>global.json</c>.
    /// </summary>
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"global.json was not found above {AppContext.BaseDirectory}.");
    }
}
