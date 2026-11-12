namespace CityBuilder.DocTools;

/// <summary>
/// The two commands of DocTools and their exit codes: <c>generate</c> rewrites the committed pages, <c>check</c> compares them with fresh ones
/// and is what CI runs ("Generated docs are current" in <c>.github/workflows/ci.yml</c>).
/// </summary>
public static class CommandLine
{
    /// <summary>The exit code of a command that did its work and, for <c>check</c>, found every page current.</summary>
    public const int Success = 0;

    /// <summary>The exit code of a <c>check</c> that found a page that differs, is missing or is extra.</summary>
    public const int PagesOutOfDate = 1;

    /// <summary>The exit code of a command line DocTools doesn't understand.</summary>
    public const int Usage = 2;

    /// <summary>What the tool prints for an unknown or missing command.</summary>
    private const string _usageText = """
        Usage: dotnet run --project tools/DocTools -- <command> [--root <repository folder>]
          generate   write docs/reference/generated/*.md from the code and the content
          check      regenerate into a temporary folder; exit 1 with a summary if a committed page differs
        The repository folder defaults to the first folder above the tool that holds global.json.
        """;

    /// <summary>Runs one command.</summary>
    /// <param name="args">The command line: the command, then optionally <c>--root</c> and a folder.</param>
    /// <param name="output">Where the command reports what it did.</param>
    /// <param name="error">Where the usage text and the check's differences go.</param>
    /// <returns><see cref="Success"/>, <see cref="PagesOutOfDate"/> or <see cref="Usage"/>.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        string? root = null;
        if (args.Count == 3 && args[1] == "--root")
        {
            root = Path.GetFullPath(args[2]);
        }
        else if (args.Count != 1)
        {
            error.WriteLine(_usageText);
            return Usage;
        }

        root ??= RepositoryRoot.Find(AppContext.BaseDirectory);
        switch (args[0])
        {
            case "generate":
                IReadOnlyList<string> written = ReferenceDocs.Write(root, ReferenceDocs.OutputFolder(root));
                output.WriteLine($"DocTools: wrote {written.Count} pages to {ReferenceDocs.RelativeOutputFolder}.");
                return Success;
            case "check":
                return GeneratedDocsCheck.Run(root, ReferenceDocs.OutputFolder(root), output, error);
            default:
                error.WriteLine(_usageText);
                return Usage;
        }
    }
}
