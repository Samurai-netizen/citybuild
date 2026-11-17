using CityBuilder.Repository.Tests.Documentation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CityBuilder.Repository.Tests.Architecture;

/// <summary>
/// Source scans that keep the player events (CR-0009) out of the game: the stored rows are reached only through the database context, the
/// table's configuration and the write-only repository, and the shared engine and the Unity client never name them. Events never feed
/// back into the game (<c>02</c> rule 23), and the client does not know they exist (the owner's decision at approval). Each scan returns
/// <c>path:line: what</c> lines; a test asserts the list is empty.
/// </summary>
internal static class MetricsSourceScan
{
    /// <summary>The server's source folder, relative to the repository root (the generated migrations are not in the list, <see cref="SourceFiles"/>).</summary>
    public const string ServerSourceFolder = "server/src/";

    /// <summary>The server sources allowed to name the stored rows: the context, the row, its configuration and the write-only repository.</summary>
    public static IReadOnlySet<string> RowFiles { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "server/src/CityBuilder.Infrastructure/Persistence/CityBuilderDbContext.cs",
        "server/src/CityBuilder.Infrastructure/Persistence/Configurations/PlayerEventConfiguration.cs",
        "server/src/CityBuilder.Infrastructure/Persistence/Repositories/PlayerEventRepository.cs",
        "server/src/CityBuilder.Infrastructure/Persistence/Rows/PlayerEventRow.cs",
    };

    /// <summary>The names in code that mean the stored rows: the context's set and the row type.</summary>
    private static readonly string[] _rowNames = ["PlayerEvents", "PlayerEventRow"];

    /// <summary>The table's name, as it appears in a string literal of the configuration.</summary>
    private const string _tableName = "player_events";

    /// <summary>The word a player event's type and members start with in C#.</summary>
    private const string _typeName = "PlayerEvent";

    /// <summary>The server's C# files as paths relative to <paramref name="root"/>, in ordinal order, migrations left out.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The paths.</returns>
    public static IReadOnlyList<string> ServerFiles(string root) =>
        SourceFiles.All(root).Where(path => path.StartsWith(ServerSourceFolder, StringComparison.Ordinal)).ToArray();

    /// <summary>The C# files of the shared engine and the Unity client, relative to <paramref name="root"/>, in ordinal order.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The paths.</returns>
    public static IReadOnlyList<string> SharedAndClientFiles(string root) =>
        SourceFiles.All(root)
            .Where(path => path.StartsWith("shared/", StringComparison.Ordinal) || path.StartsWith("client/", StringComparison.Ordinal))
            .ToArray();

    /// <summary>Every use of the stored rows in code (the <c>PlayerEvents</c> set, the <c>PlayerEventRow</c> type, the <c>player_events</c> table name) in a file not in <see cref="RowFiles"/>.</summary>
    /// <param name="path">The file's path, relative to the repository root.</param>
    /// <param name="source">The file's text.</param>
    /// <returns>One line per use; comments don't count.</returns>
    public static IReadOnlyList<string> RowUses(string path, string source)
    {
        if (RowFiles.Contains(path))
        {
            return [];
        }

        SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();
        IEnumerable<string> names = root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(name => _rowNames.Contains(name.Identifier.ValueText, StringComparer.Ordinal))
            .Select(name => Report(path, name, $"'{name.Identifier.ValueText}' (only the write-only IPlayerEventRepository reaches the events)"));
        IEnumerable<string> tables = root.DescendantNodes().OfType<LiteralExpressionSyntax>()
            .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText.Contains(_tableName, StringComparison.Ordinal))
            .Select(literal => Report(path, literal, $"the table name \"{_tableName}\" (only its configuration names it)"));
        return names.Concat(tables).ToArray();
    }

    /// <summary>Every line that names a player event (<c>PlayerEvent</c> or <c>player_events</c>), in code or in a comment.</summary>
    /// <param name="path">The file's path, relative to the repository root.</param>
    /// <param name="source">The file's text.</param>
    /// <returns>One line per line that names one.</returns>
    public static IReadOnlyList<string> Mentions(string path, string source)
    {
        string[] lines = source.Split('\n');
        var found = new List<string>();
        for (int index = 0; index < lines.Length; index++)
        {
            if (lines[index].Contains(_typeName, StringComparison.Ordinal) || lines[index].Contains(_tableName, StringComparison.Ordinal))
            {
                found.Add($"{path}:{index + 1}: names a player event");
            }
        }

        return found;
    }

    /// <summary>One report line: <c>path:line: message</c>.</summary>
    private static string Report(string path, SyntaxNode node, string message) =>
        $"{path}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {message}";
}
