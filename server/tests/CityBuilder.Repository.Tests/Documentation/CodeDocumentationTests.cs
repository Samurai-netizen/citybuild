namespace CityBuilder.Repository.Tests.Documentation;

/// <summary>
/// The repository-wide check of <c>core-instructions/14-coding-standards.md</c> → Documentation in code: every C# file describes itself
/// and every declaration in it, at every access level. A failure lists each problem as <c>path:line: …</c>; the how-to
/// <c>docs/how-to/document-code.md</c> shows how to fix each kind.
/// </summary>
public sealed class CodeDocumentationTests
{
    /// <summary>
    /// Folders that CR-0003-T1 has not documented yet. Each step of that task removes the folders it finished, and its last step removes
    /// this list, after which nothing is skipped but the exempt folders of <see cref="SourceFiles"/>.
    /// </summary>
    private static readonly string[] _notYetDocumented =
    [
        "client/CityBuilder/Assets/_Project",
        "server/tests/CityBuilder.Application.Tests",
        "server/tests/CityBuilder.Domain.Tests",
        "server/tests/CityBuilder.IntegrationTests",
    ];

    /// <summary>Every C# file of the repository passes every rule of <see cref="DocumentationRules"/>; the message lists all problems at once.</summary>
    [Fact]
    public void Check_EveryCSharpFileOfTheRepository_ReportsNoProblem()
    {
        string root = RepositoryPaths.Root;

        DocumentationProblem[] problems = SourceFiles.All(root)
            .Where(path => !_notYetDocumented.Any(folder => path.StartsWith(folder + "/", StringComparison.Ordinal)))
            .SelectMany(path => DocumentationRules.Check(path, File.ReadAllText(Path.Combine(root, path))))
            .ToArray();

        Assert.True(problems.Length == 0,
            $"{problems.Length} documentation problem(s); see core-instructions/14-coding-standards.md → Documentation in code:\n"
            + string.Join("\n", problems.Select(problem => problem.ToString())));
    }
}
