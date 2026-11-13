using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CityBuilder.Repository.Tests.Architecture;

/// <summary>
/// Syntax scans of the shared engine's sources (<c>shared/Simulation/Runtime/</c>) for the rules of
/// <c>.claude/rules/simulation.md</c> that the compiler can't enforce: integers only, no clock or randomness, and no content ids or era
/// numbers in code (<c>core-instructions/02-principles.md</c>, rules 6 and 8). Each scan returns <c>path:line: what</c> lines; a test
/// asserts the list is empty. The scans read syntax only, so comments and doc comments never count and text in strings does.
/// </summary>
internal static class EngineSourceScan
{
    /// <summary>The engine's source folder, relative to the repository root.</summary>
    private const string _engineFolder = "shared/Simulation/Runtime/";

    /// <summary>Type names that mean time or randomness: the engine has no clock (<c>DateTime</c>, <c>TimeProvider</c>), no sleeping and no randomness.</summary>
    private static readonly string[] _timeAndRandomNames = ["DateTime", "DateTimeOffset", "TimeProvider", "Stopwatch", "Thread", "Random", "Guid"];

    /// <summary>The engine's C# files as paths relative to <paramref name="root"/>, in ordinal order.</summary>
    public static IReadOnlyList<string> Files(string root) =>
        Documentation.SourceFiles.All(root).Where(path => path.StartsWith(_engineFolder, StringComparison.Ordinal)).ToArray();

    /// <summary>Every <c>float</c>, <c>double</c> or <c>decimal</c> keyword in the given source: fields, properties, parameters, locals and casts alike.</summary>
    public static IReadOnlyList<string> FloatingPointTypes(string path, string source) =>
        Parse(source).GetRoot().DescendantNodes().OfType<PredefinedTypeSyntax>()
            .Where(type => type.Keyword.Kind() is SyntaxKind.FloatKeyword or SyntaxKind.DoubleKeyword or SyntaxKind.DecimalKeyword)
            .Select(type => Report(path, type, $"'{type.Keyword.Text}' (integers only, 04 §1)"))
            .ToArray();

    /// <summary>Every name of a clock, a sleep or a source of randomness (<see cref="_timeAndRandomNames"/>) used as an identifier.</summary>
    public static IReadOnlyList<string> TimeAndRandomness(string path, string source) =>
        Parse(source).GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(name => _timeAndRandomNames.Contains(name.Identifier.ValueText, StringComparer.Ordinal))
            .Select(name => Report(path, name, $"'{name.Identifier.ValueText}' (no clock or randomness in the engine)"))
            .ToArray();

    /// <summary>
    /// Every string literal equal to one of <paramref name="contentIds"/> (a research, era, building or item id), and every comparison
    /// of an era (a name containing <c>Era</c>) with a number, such as <c>era == 2</c>. Era, research and effects are data (<c>04</c>).
    /// </summary>
    public static IReadOnlyList<string> ContentLiterals(string path, string source, IReadOnlySet<string> contentIds)
    {
        SyntaxNode root = Parse(source).GetRoot();
        var found = new List<string>();

        foreach (LiteralExpressionSyntax literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>()
                     .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression)))
        {
            if (contentIds.Contains(literal.Token.ValueText))
            {
                found.Add(Report(path, literal, $"the content id \"{literal.Token.ValueText}\" (game data lives in server/content/*.json)"));
            }
        }

        foreach (BinaryExpressionSyntax comparison in root.DescendantNodes().OfType<BinaryExpressionSyntax>()
                     .Where(IsComparison))
        {
            if ((MentionsEra(comparison.Left) && IsNumber(comparison.Right)) || (IsNumber(comparison.Left) && MentionsEra(comparison.Right)))
            {
                found.Add(Report(path, comparison, $"a comparison of an era with a number: {comparison}"));
            }
        }

        return found;
    }

    /// <summary>
    /// The ids of every kind of content in <c>server/content/mvp-content.json</c>: eras, items, buildings, research and amount presets.
    /// An id that is also a building <c>kind</c> (the file's own vocabulary, which the content reader names, such as <c>house</c>) is left
    /// out, because the same word then has a second meaning that code may spell.
    /// </summary>
    public static IReadOnlySet<string> ContentIds(string root)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "server/content/mvp-content.json")));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string section in new[] { "eras", "items", "buildings", "research", "amountPresets" })
        {
            foreach (JsonElement entry in document.RootElement.GetProperty(section).EnumerateArray())
            {
                ids.Add(entry.GetProperty("id").GetString()!);
            }
        }

        foreach (JsonElement building in document.RootElement.GetProperty("buildings").EnumerateArray())
        {
            if (building.TryGetProperty("kind", out JsonElement kind) && kind.ValueKind == JsonValueKind.String)
            {
                ids.Remove(kind.GetString()!);
            }
        }

        return ids;
    }

    /// <summary>Parses C# source into a syntax tree (syntax only: nothing is compiled or bound).</summary>
    private static SyntaxTree Parse(string source) => CSharpSyntaxTree.ParseText(source);

    /// <summary>True for the operators that compare two values: <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>.</summary>
    private static bool IsComparison(BinaryExpressionSyntax expression) =>
        expression.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;

    /// <summary>
    /// True when the expression is a number written in the code, other than 0 or 1: those two are the bounds of input checks such as
    /// <c>era &lt; 1</c> ("an era number starts at 1"), not a particular era. Any other number names one era, which must be data.
    /// </summary>
    private static bool IsNumber(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NumericLiteralExpression)
        && literal.Token.ValueText is not ("0" or "1");

    /// <summary>True when the expression is a name, or a member access ending in a name, that contains <c>Era</c> (for example <c>era</c> or <c>city.Era</c>).</summary>
    private static bool MentionsEra(ExpressionSyntax expression)
    {
        string name = expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => string.Empty,
        };
        return name.Contains("era", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One report line: <c>path:line: message</c>.</summary>
    private static string Report(string path, SyntaxNode node, string message) =>
        $"{path}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {message}";
}
