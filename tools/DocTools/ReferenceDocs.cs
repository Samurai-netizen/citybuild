using System.Text;
using CityBuilder.DocTools.Pages;

namespace CityBuilder.DocTools;

/// <summary>The set of generated reference pages: which generators run, and where their pages are written.</summary>
public static class ReferenceDocs
{
    /// <summary>The folder of the generated pages, relative to the repository root.</summary>
    public const string RelativeOutputFolder = "docs/reference/generated";

    /// <summary>UTF-8 without a byte order mark, as every other text file of the repository.</summary>
    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The committed folder of the generated pages.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>Its full path.</returns>
    public static string OutputFolder(string root) => Path.Combine(root, RelativeOutputFolder);

    /// <summary>Runs every generator.</summary>
    /// <param name="root">The repository root, which the generators read from.</param>
    /// <returns>The pages, in ordinal order of their file names.</returns>
    public static IReadOnlyList<GeneratedPage> Generate(string root) =>
        [.. new[] { ContentPage.Generate(root), ErrorsPage.Generate(), ProjectMapPage.Generate(root) }.OrderBy(page => page.FileName, StringComparer.Ordinal)];

    /// <summary>
    /// Generates every page into <paramref name="folder"/> and deletes the <c>*.md</c> files there that no generator makes any more, so the
    /// folder holds exactly the current set.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="folder">Where the pages go; created when missing.</param>
    /// <returns>The file names written, in ordinal order.</returns>
    public static IReadOnlyList<string> Write(string root, string folder)
    {
        IReadOnlyList<GeneratedPage> pages = Generate(root);
        Directory.CreateDirectory(folder);
        var names = new HashSet<string>(pages.Select(page => page.FileName), StringComparer.Ordinal);
        foreach (string stale in Directory.GetFiles(folder, "*.md").Where(file => !names.Contains(Path.GetFileName(file))))
        {
            File.Delete(stale);
        }

        foreach (GeneratedPage page in pages)
        {
            File.WriteAllText(Path.Combine(folder, page.FileName), page.Text, _utf8);
        }

        return [.. pages.Select(page => page.FileName)];
    }
}
