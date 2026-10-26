namespace CityBuilder.Repository.Tests.Documentation;

/// <summary>
/// The C# files the documentation rules apply to: every one in the repository's code folders, except build output, the caches Unity
/// and IDEs make, and the few folders whose files are not this project's own.
/// </summary>
/// <remarks>
/// The search reads the folders on disk, not git's index, so a new file is checked before it is first committed. Folders are walked in
/// ordinal order, which makes the list, and every report built from it, the same on every machine.
/// </remarks>
internal static class SourceFiles
{
    /// <summary>The folders searched, relative to the repository root: the server, the shared engine, the Unity project's assets and the tools.</summary>
    private static readonly string[] _searchedFolders = ["server", "shared", "client/CityBuilder/Assets", "tools"];

    /// <summary>Folder names that are never searched, wherever they appear: build output (<c>bin</c>, <c>obj</c>) and generated or cached folders.</summary>
    private static readonly HashSet<string> _skippedFolderNames = new(StringComparer.Ordinal)
    {
        "bin", "obj", "Library", "Temp", "Logs", "UserSettings", "node_modules", ".vs", ".idea", ".vscode",
    };

    /// <summary>
    /// Folders, relative to the repository root, whose files the rules don't apply to because the project didn't write them: the
    /// sample scripts of Unity's project template (<c>Assets/TutorialInfo/</c>), which the owner may remove (an open question since M0-04),
    /// and the EF Core migrations that dotnet ef generates and nobody edits by hand (docs/how-to/add-migration.md; the owner's choice in
    /// M0-19, decision 9 of its build log).
    /// </summary>
    public static IReadOnlyList<string> ExemptFolders { get; } =
        ["client/CityBuilder/Assets/TutorialInfo", "server/src/CityBuilder.Infrastructure/Persistence/Migrations"];

    /// <summary>Every C# file to check, as paths relative to <paramref name="root"/> with <c>/</c> separators, in ordinal order.</summary>
    /// <param name="root">The repository root, normally <see cref="RepositoryPaths.Root"/>.</param>
    public static IReadOnlyList<string> All(string root)
    {
        var files = new List<string>();
        foreach (string folder in _searchedFolders)
        {
            string absolute = Path.Combine(root, folder);
            if (Directory.Exists(absolute))
            {
                Collect(root, absolute, files);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>Adds the C# files of one folder and of its subfolders to <paramref name="files"/>, skipping the folders named above.</summary>
    private static void Collect(string root, string folder, List<string> files)
    {
        string relativeFolder = Relative(root, folder);
        if (ExemptFolders.Any(exempt => relativeFolder == exempt || relativeFolder.StartsWith(exempt + "/", StringComparison.Ordinal)))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(folder, "*.cs").Order(StringComparer.Ordinal))
        {
            files.Add(Relative(root, file));
        }

        foreach (string subfolder in Directory.GetDirectories(folder).Order(StringComparer.Ordinal))
        {
            if (!_skippedFolderNames.Contains(Path.GetFileName(subfolder)))
            {
                Collect(root, subfolder, files);
            }
        }
    }

    /// <summary>A path relative to the repository root with <c>/</c> separators, the form every report and exemption uses on every system.</summary>
    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
}
