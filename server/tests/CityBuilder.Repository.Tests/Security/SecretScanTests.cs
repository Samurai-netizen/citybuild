using System.Text.Json;

namespace CityBuilder.Repository.Tests.Security;

/// <summary>
/// The "Secret leakage" threat of <c>core-instructions/07-security-model.md</c> (M0-81): the signing key and the database connection string
/// come from user-secrets or the environment only, never from a committed file. This scans what is committed: the server's and the tools'
/// settings files, the Unity client's code, settings and data, and the ignore rule that keeps the compose password's <c>.env</c> out of git.
/// </summary>
public sealed class SecretScanTests
{
    /// <summary>The folders whose <c>appsettings*.json</c> files are scanned, relative to the repository root.</summary>
    private static readonly string[] _settingsFolders = ["server/src", "tools"];

    /// <summary>The Unity client's folders that ship in a build or configure it, relative to the repository root.</summary>
    private static readonly string[] _clientFolders = ["client/CityBuilder/Assets", "client/CityBuilder/ProjectSettings"];

    /// <summary>The kinds of client file that can hold a value as text: code, data and Unity's serialized settings.</summary>
    private static readonly string[] _clientExtensions = [".cs", ".json", ".asset", ".txt"];

    /// <summary>Text that only a server secret contains: the signing key's setting and a connection string's password.</summary>
    private static readonly string[] _secretMarkers = ["SigningKey", "Password="];

    /// <summary>
    /// No committed settings file sets <c>Auth:SigningKey</c> or any connection string: both must come from user-secrets or the environment,
    /// and the start fails without them (<c>Start_NoOrShortSigningKey_FailsValidation</c>, <c>Start_NoConnectionString_FailsValidation</c>).
    /// </summary>
    [Fact]
    public void Security_CommittedSettings_SetNoSigningKeyOrConnectionString()
    {
        string[] files = [.. _settingsFolders.SelectMany(folder => Files(folder, "appsettings*.json"))];
        Assert.NotEmpty(files);

        string[] found = [.. files.SelectMany(file => SecretsIn(file).Select(name => $"{Relative(file)}: {name}"))];

        Assert.Empty(found);
    }

    /// <summary>The Unity client holds no server secret: no file of its code, data or settings names the signing key or a password.</summary>
    [Fact]
    public void Security_UnityClient_HoldsNoServerSecret()
    {
        string[] files = [.. _clientFolders.SelectMany(folder => Files(folder, "*")).Where(file => _clientExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))];
        Assert.NotEmpty(files);

        string[] found =
        [
            .. files.SelectMany(file =>
            {
                string text = File.ReadAllText(file);
                return _secretMarkers.Where(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)).Select(marker => $"{Relative(file)}: {marker}");
            }),
        ];

        Assert.Empty(found);
    }

    /// <summary>The compose password's <c>.env</c> file is ignored by git, so a copy of <c>.env.example</c> with a real password is never committed.</summary>
    [Fact]
    public void Security_DotEnv_IsIgnoredByGit()
    {
        string[] rules = File.ReadAllLines(Path.Combine(RepositoryPaths.Root, ".gitignore"));

        Assert.Contains(rules, rule => rule.Trim() is ".env" or "/.env");
    }

    /// <summary>Every file below a folder of the repository that matches a pattern, without build output (<c>bin</c>, <c>obj</c>) or Unity's caches.</summary>
    /// <param name="folder">The folder, relative to the repository root.</param>
    /// <param name="pattern">The file name pattern.</param>
    /// <returns>The full paths.</returns>
    private static IEnumerable<string> Files(string folder, string pattern)
    {
        string root = Path.Combine(RepositoryPaths.Root, folder);
        string[] skipped = ["bin", "obj", "Library", "Temp", "node_modules"];
        return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar).Any(part => skipped.Contains(part, StringComparer.Ordinal)));
    }

    /// <summary>The names of the secret settings a settings file sets: a non-empty <c>SigningKey</c> anywhere, and every connection string.</summary>
    /// <param name="file">The settings file.</param>
    /// <returns>The paths of the settings, like <c>Auth:SigningKey</c>.</returns>
    private static IEnumerable<string> SecretsIn(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var found = new List<string>();
        Walk(document.RootElement, path: "", found);
        return found;
    }

    /// <summary>Collects every secret setting below one JSON element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="path">Its setting path, empty for the root.</param>
    /// <param name="found">Receives the paths of the secret settings.</param>
    private static void Walk(JsonElement element, string path, List<string> found)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            string name = path.Length == 0 ? property.Name : $"{path}:{property.Name}";
            bool signingKey = property.Name.Equals("SigningKey", StringComparison.OrdinalIgnoreCase)
                && !(property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()!.Length == 0);
            bool connectionString = path.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase);
            if (signingKey || connectionString)
            {
                found.Add(name);
            }

            Walk(property.Value, name, found);
        }
    }

    /// <summary>A path relative to the repository root, for the failure message.</summary>
    /// <param name="file">The full path.</param>
    /// <returns>The relative path.</returns>
    private static string Relative(string file) => Path.GetRelativePath(RepositoryPaths.Root, file);
}
