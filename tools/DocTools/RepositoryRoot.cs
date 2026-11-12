namespace CityBuilder.DocTools;

/// <summary>Finds the repository root, the folder every generator reads from and every page path is relative to.</summary>
public static class RepositoryRoot
{
    /// <summary>
    /// The first folder at or above <paramref name="start"/> that holds <c>global.json</c>: the repository root, wherever the tool is
    /// built (<c>tools/DocTools/bin/…</c>) and whatever the current folder is.
    /// </summary>
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

    /// <summary>A path relative to the root with <c>/</c> separators, as the pages show it on every machine.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="path">A path inside the repository.</param>
    /// <returns>The relative path, for example <c>server/src/CityBuilder.Api/CityBuilder.Api.csproj</c>.</returns>
    public static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
}
