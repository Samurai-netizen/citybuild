namespace CityBuilder.BalanceSim;

/// <summary>
/// Finds the repository root, against which the default content file, the default output folder and relative paths are resolved. DocTools
/// has the same ten lines (tools/DocTools/RepositoryRoot.cs); the tools share no project, so each keeps its own.
/// </summary>
public static class RepositoryRoot
{
    /// <summary>The first folder at or above <paramref name="start"/> that holds <c>global.json</c>.</summary>
    /// <param name="start">The folder to start from, normally the tool's base directory.</param>
    /// <returns>The root's full path.</returns>
    /// <exception cref="InvalidOperationException">No folder above <paramref name="start"/> holds <c>global.json</c>.</exception>
    public static string Find(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"global.json was not found above {start}.");
    }
}
