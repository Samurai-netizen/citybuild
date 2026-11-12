namespace CityBuilder.DocTools;

/// <summary>How a committed page differs from the freshly generated one.</summary>
public enum DifferenceKind
{
    /// <summary>Both exist and their texts differ.</summary>
    Changed,

    /// <summary>The generator makes the page, but it isn't committed.</summary>
    Missing,

    /// <summary>A page is committed that the generator no longer makes.</summary>
    Extra,
}

/// <summary>One page of <c>docs/reference/generated/</c> that is not current, with the first line where it differs.</summary>
/// <param name="FileName">The page's file name.</param>
/// <param name="Kind">How it differs.</param>
/// <param name="Line">The first differing line (from 1), for <see cref="DifferenceKind.Changed"/>; 0 otherwise.</param>
/// <param name="Committed">The committed text of that line; empty when the committed page ends before it.</param>
/// <param name="Generated">The generated text of that line; empty when the generated page ends before it.</param>
/// <param name="ChangedLines">How many lines of the longer page differ from the line at the same place in the other.</param>
public sealed record PageDifference(string FileName, DifferenceKind Kind, int Line, string Committed, string Generated, int ChangedLines)
{
    /// <summary>The most characters of a line the summary shows.</summary>
    private const int _maxShownCharacters = 160;

    /// <summary>One or three lines for the check's report.</summary>
    /// <returns>The summary, for example <c>  changed  content.md: 3 line(s) differ, first at line 12</c> and the two versions of that line.</returns>
    public string Describe() => Kind switch
    {
        DifferenceKind.Missing => $"  missing  {FileName}: generated but not committed",
        DifferenceKind.Extra => $"  extra    {FileName}: committed but no longer generated",
        _ => $"  changed  {FileName}: {ChangedLines} line(s) differ, first at line {Line}\n" +
             $"           committed: {Shorten(Committed)}\n" +
             $"           generated: {Shorten(Generated)}",
    };

    /// <summary>A line cut to <see cref="_maxShownCharacters"/> characters, so one long table row doesn't flood the CI log.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The line, or its start and an ellipsis.</returns>
    private static string Shorten(string line) => line.Length <= _maxShownCharacters ? line : line[.._maxShownCharacters] + "…";
}
