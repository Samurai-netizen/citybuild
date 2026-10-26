namespace CityBuilder.Repository.Tests.Documentation;

/// <summary>One missing, empty or misplaced description that <see cref="DocumentationRules"/> found in a C# file.</summary>
/// <param name="Path">The file's path relative to the repository root, with <c>/</c> separators.</param>
/// <param name="Line">The 1-based line of the declaration's name (or of the file's first line for a file-level problem).</param>
/// <param name="Kind">What is affected: <c>class</c>, <c>method</c>, <c>field</c>, <c>local function</c>, <c>file</c>, and so on.</param>
/// <param name="Name">The affected declaration's name, such as <c>Advance</c> or <c>this[]</c>.</param>
/// <param name="Message">What is wrong, as the end of a sentence that starts with the kind and the name.</param>
internal sealed record DocumentationProblem(string Path, int Line, string Kind, string Name, string Message)
{
    /// <summary>The problem as one line, <c>path:line: kind name message</c>, a form that editors and CI logs turn into a link.</summary>
    public override string ToString() => $"{Path}:{Line}: {Kind} {Name} {Message}";
}
