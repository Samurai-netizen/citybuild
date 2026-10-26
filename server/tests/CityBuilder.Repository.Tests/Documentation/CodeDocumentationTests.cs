namespace CityBuilder.Repository.Tests.Documentation;

/// <summary>
/// The repository-wide check of <c>core-instructions/14-coding-standards.md</c> → Documentation in code: every C# file describes itself
/// and every declaration in it, at every access level. A failure lists each problem as <c>path:line: …</c>; the how-to
/// <c>docs/how-to/document-code.md</c> shows how to fix each kind.
/// </summary>
public sealed class CodeDocumentationTests
{
    /// <summary>
    /// Every C# file of the repository passes every rule of <see cref="DocumentationRules"/>; only the exempt folders of
    /// <see cref="SourceFiles"/> are skipped. The message lists all problems at once.
    /// </summary>
    [Fact]
    public void Check_EveryCSharpFileOfTheRepository_ReportsNoProblem()
    {
        string root = RepositoryPaths.Root;

        DocumentationProblem[] problems = SourceFiles.All(root)
            .SelectMany(path => DocumentationRules.Check(path, File.ReadAllText(Path.Combine(root, path))))
            .ToArray();

        Assert.True(problems.Length == 0,
            $"{problems.Length} documentation problem(s); see core-instructions/14-coding-standards.md → Documentation in code:\n"
            + string.Join("\n", problems.Select(problem => problem.ToString())));
    }
}
