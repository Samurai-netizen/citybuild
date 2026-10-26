namespace CityBuilder.Repository.Tests;

/// <summary>Finds the repository's root folder from the folder the tests run in, so the tests can read the source files.</summary>
/// <remarks>
/// <c>CityBuilder.Simulation.Tests</c> has a twin of this class for its own file checks; two copies of a dozen lines are simpler than a
/// shared test-helpers project (<c>core-instructions/14-coding-standards.md</c>: no abstraction without real duplication).
/// </remarks>
internal static class RepositoryPaths
{
    /// <summary>
    /// The repository root. The tests run from <c>server/tests/&lt;project&gt;/bin/&lt;configuration&gt;/&lt;framework&gt;/</c>,
    /// so it is found by walking up to the folder that holds <c>global.json</c>.
    /// </summary>
    public static string Root { get; } = FindRoot();

    /// <summary>Walks up from the test binaries to the first folder with a <c>global.json</c>; throws when there is none, because then the tests can't run.</summary>
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
