namespace CityBuilder.DocTools;

/// <summary>Compares the committed reference pages with freshly generated ones (the <c>check</c> command; "docs out of date" in CI).</summary>
public static class GeneratedDocsCheck
{
    /// <summary>
    /// The <c>check</c> command: regenerates the pages into a new temporary folder, compares them with <paramref name="committedFolder"/>,
    /// reports the result and deletes the temporary folder again.
    /// </summary>
    /// <param name="root">The repository root the generators read.</param>
    /// <param name="committedFolder">The committed pages, normally <see cref="ReferenceDocs.OutputFolder"/>.</param>
    /// <param name="output">Where the result goes when every page is current.</param>
    /// <param name="error">Where the differences go.</param>
    /// <returns><see cref="CommandLine.Success"/> or <see cref="CommandLine.PagesOutOfDate"/>.</returns>
    public static int Run(string root, string committedFolder, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        string fresh = Directory.CreateTempSubdirectory("doctools-").FullName;
        try
        {
            ReferenceDocs.Write(root, fresh);
            IReadOnlyList<PageDifference> differences = Compare(committedFolder, fresh);
            if (differences.Count == 0)
            {
                output.WriteLine($"DocTools: every page in {ReferenceDocs.RelativeOutputFolder} is current.");
                return CommandLine.Success;
            }

            error.WriteLine($"DocTools: docs out of date: {differences.Count} page(s) in {ReferenceDocs.RelativeOutputFolder} differ from the code.");
            foreach (PageDifference difference in differences)
            {
                error.WriteLine(difference.Describe());
            }

            error.WriteLine("Run 'dotnet run --project tools/DocTools -- generate' and commit the result.");
            return CommandLine.PagesOutOfDate;
        }
        finally
        {
            Directory.Delete(fresh, recursive: true);
        }
    }

    /// <summary>Every page that is changed, missing or extra, in ordinal order of the file names. Only <c>*.md</c> files count.</summary>
    /// <param name="committedFolder">The committed pages, <c>docs/reference/generated/</c>; a folder that doesn't exist holds no pages.</param>
    /// <param name="generatedFolder">The fresh pages.</param>
    /// <returns>The differences; empty when every page is current.</returns>
    public static IReadOnlyList<PageDifference> Compare(string committedFolder, string generatedFolder)
    {
        SortedSet<string> committed = PagesIn(committedFolder);
        SortedSet<string> generated = PagesIn(generatedFolder);
        var differences = new List<PageDifference>();
        foreach (string name in committed.Union(generated).Order(StringComparer.Ordinal))
        {
            if (!committed.Contains(name))
            {
                differences.Add(new PageDifference(name, DifferenceKind.Missing, 0, string.Empty, string.Empty, 0));
            }
            else if (!generated.Contains(name))
            {
                differences.Add(new PageDifference(name, DifferenceKind.Extra, 0, string.Empty, string.Empty, 0));
            }
            else if (CompareTexts(name, File.ReadAllText(Path.Combine(committedFolder, name)), File.ReadAllText(Path.Combine(generatedFolder, name)))
                     is { } changed)
            {
                differences.Add(changed);
            }
        }

        return differences;
    }

    /// <summary>
    /// The difference between two texts of one page, line by line, or null when they are equal. Lines are split at <c>\n</c> only, so a
    /// page with CRLF line ends (a checkout that converts them) differs on every line.
    /// </summary>
    /// <param name="name">The page's file name.</param>
    /// <param name="committed">The committed text.</param>
    /// <param name="generated">The generated text.</param>
    /// <returns>A <see cref="DifferenceKind.Changed"/> difference, or null.</returns>
    public static PageDifference? CompareTexts(string name, string committed, string generated)
    {
        ArgumentNullException.ThrowIfNull(committed);
        ArgumentNullException.ThrowIfNull(generated);
        if (string.Equals(committed, generated, StringComparison.Ordinal))
        {
            return null;
        }

        // The final line end ends the last line; it doesn't start an empty one.
        string[] committedLines = LinesOf(committed);
        string[] generatedLines = LinesOf(generated);
        int first = 0;
        int changed = 0;
        for (int i = 0; i < Math.Max(committedLines.Length, generatedLines.Length); i++)
        {
            string left = i < committedLines.Length ? committedLines[i] : string.Empty;
            string right = i < generatedLines.Length ? generatedLines[i] : string.Empty;
            if (!string.Equals(left, right, StringComparison.Ordinal) || (i >= committedLines.Length) != (i >= generatedLines.Length))
            {
                changed++;
                if (first == 0)
                {
                    first = i + 1;
                }
            }
        }

        string committedLine = first <= committedLines.Length ? committedLines[first - 1] : string.Empty;
        string generatedLine = first <= generatedLines.Length ? generatedLines[first - 1] : string.Empty;
        return new PageDifference(name, DifferenceKind.Changed, first, committedLine, generatedLine, changed);
    }

    /// <summary>A text's lines, split at <c>\n</c>; one final line end doesn't add an empty line.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The lines.</returns>
    private static string[] LinesOf(string text) => (text.EndsWith('\n') ? text[..^1] : text).Split('\n');

    /// <summary>The <c>*.md</c> file names directly in a folder.</summary>
    /// <param name="folder">The folder; one that doesn't exist has none.</param>
    /// <returns>The names, ordinal.</returns>
    private static SortedSet<string> PagesIn(string folder) =>
        Directory.Exists(folder)
            ? new SortedSet<string>(Directory.GetFiles(folder, "*.md").Select(Path.GetFileName).OfType<string>(), StringComparer.Ordinal)
            : new SortedSet<string>(StringComparer.Ordinal);
}
