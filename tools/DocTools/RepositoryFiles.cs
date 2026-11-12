namespace CityBuilder.DocTools;

/// <summary>Finds the repository's own files of one kind, the same set in the same order on every machine.</summary>
public static class RepositoryFiles
{
    /// <summary>Folder names that never hold the project's own files: build output and the caches of Unity, Node and the IDEs.</summary>
    private static readonly HashSet<string> _skipped = new(StringComparer.Ordinal)
    {
        "bin", "obj", "Library", "Temp", "Logs", "UserSettings", "node_modules", ".git", ".vs", ".idea", ".vscode", ".utmp",
    };

    /// <summary>Every file matching <paramref name="pattern"/> under the given folders, skipping the folders above.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="folders">The folders to search, relative to the root; a missing one is skipped.</param>
    /// <param name="pattern">A file pattern, for example <c>*.csproj</c>.</param>
    /// <returns>Paths relative to the root with <c>/</c> separators, ordinal.</returns>
    public static IReadOnlyList<string> Find(string root, IEnumerable<string> folders, string pattern)
    {
        var found = new List<string>();
        foreach (string folder in folders)
        {
            string absolute = Path.Combine(root, folder);
            if (Directory.Exists(absolute))
            {
                Collect(root, absolute, pattern, found);
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>Adds the matching files of one folder and its subfolders.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="folder">The folder's full path.</param>
    /// <param name="pattern">The file pattern.</param>
    /// <param name="found">The list to add to.</param>
    private static void Collect(string root, string folder, string pattern, List<string> found)
    {
        found.AddRange(Directory.GetFiles(folder, pattern).Select(file => RepositoryRoot.Relative(root, file)));
        foreach (string child in Directory.GetDirectories(folder).Where(child => !_skipped.Contains(Path.GetFileName(child))))
        {
            Collect(root, child, pattern, found);
        }
    }
}
