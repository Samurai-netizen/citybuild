using CityBuilder.Repository.Tests.Documentation;

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
    public static IReadOnlySet<string> RowFiles { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The server's C# files as paths relative to <paramref name="root"/>, in ordinal order, migrations left out.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The paths.</returns>
    public static IReadOnlyList<string> ServerFiles(string root) => [];

    /// <summary>The C# files of the shared engine and the Unity client, relative to <paramref name="root"/>, in ordinal order.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The paths.</returns>
    public static IReadOnlyList<string> SharedAndClientFiles(string root) => [];

    /// <summary>Every use of the stored rows in code (the <c>PlayerEvents</c> set, the <c>PlayerEventRow</c> type, the <c>player_events</c> table name) in a file not in <see cref="RowFiles"/>.</summary>
    /// <param name="path">The file's path, relative to the repository root.</param>
    /// <param name="source">The file's text.</param>
    /// <returns>One line per use; comments don't count.</returns>
    public static IReadOnlyList<string> RowUses(string path, string source) => [];

    /// <summary>Every line that names a player event (<c>PlayerEvent</c> or <c>player_events</c>), in code or in a comment.</summary>
    /// <param name="path">The file's path, relative to the repository root.</param>
    /// <param name="source">The file's text.</param>
    /// <returns>One line per line that names one.</returns>
    public static IReadOnlyList<string> Mentions(string path, string source) => [];
}
