namespace CityBuilder.DocTools;

/// <summary>One generated reference page: its file name in <c>docs/reference/generated/</c> and its full text.</summary>
/// <param name="FileName">The file name, for example <c>content.md</c>.</param>
/// <param name="Text">The Markdown, with <c>\n</c> line ends and the generated-file notice first.</param>
public sealed record GeneratedPage(string FileName, string Text);
