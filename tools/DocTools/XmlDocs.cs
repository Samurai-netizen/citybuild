using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CityBuilder.DocTools;

/// <summary>
/// Reads the summaries of an assembly's XML documentation file (the build writes one for every project) as Markdown, so a page can show
/// what a member's doc comment says. A reference to a constant shows the constant's value, so "between <c>MinX</c> and <c>MaxX</c>" reads as
/// numbers.
/// </summary>
public sealed partial class XmlDocs
{
    /// <summary>The summaries by member id (<c>P:Namespace.Type.Property</c>), as written in the file.</summary>
    private readonly Dictionary<string, XElement> _summaries;

    /// <summary>The assembly the documentation belongs to, for the constants its references name.</summary>
    private readonly Assembly _assembly;

    /// <summary>Reads the documentation file next to an assembly.</summary>
    /// <param name="assembly">The assembly; its <c>.xml</c> file must be beside it (the build copies it with the assembly).</param>
    /// <exception cref="FileNotFoundException">There is no documentation file.</exception>
    public XmlDocs(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _assembly = assembly;
        string path = Path.ChangeExtension(assembly.Location, ".xml");
        XDocument file = XDocument.Load(path);
        _summaries = file.Descendants("member")
            .Where(member => member.Element("summary") is not null)
            .ToDictionary(member => (string)member.Attribute("name")!, member => member.Element("summary")!, StringComparer.Ordinal);
    }

    /// <summary>A property's summary as one line of Markdown, or an empty string when it has none.</summary>
    /// <param name="property">The property.</param>
    /// <returns>The text: <c>c</c> elements become inline code, references become the referenced name or constant value.</returns>
    public string Summary(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        string id = "P:" + property.DeclaringType!.FullName!.Replace('+', '.') + "." + property.Name;
        return _summaries.TryGetValue(id, out XElement? summary) ? Render(summary) : string.Empty;
    }

    /// <summary>A summary's nodes as Markdown on one line.</summary>
    /// <param name="summary">The summary element.</param>
    /// <returns>The text.</returns>
    private string Render(XElement summary)
    {
        string text = string.Concat(summary.Nodes().Select(RenderNode));
        return Whitespace().Replace(text, " ").Trim();
    }

    /// <summary>One node: text as it is (escaped for the page), a <c>c</c> element as code, a <c>see</c> element as its name or value.</summary>
    /// <param name="node">The node.</param>
    /// <returns>Its Markdown.</returns>
    private string RenderNode(XNode node) => node switch
    {
        XText text => MarkdownPage.Text(text.Value),
        XElement { Name.LocalName: "c" } code => MarkdownPage.Code(code.Value),
        XElement { Name.LocalName: "see" or "seealso" } see when see.Attribute("langword") is { } word => MarkdownPage.Code(word.Value),
        XElement { Name.LocalName: "see" or "seealso" or "paramref" or "typeparamref" } see => Reference(see),
        XElement other => string.Concat(other.Nodes().Select(RenderNode)),
        _ => string.Empty,
    };

    /// <summary>
    /// A reference: a constant of the assembly shows its value, anything else its short name as code. A reference with its own text shows that.
    /// </summary>
    /// <param name="see">The <c>see</c>, <c>paramref</c> or <c>typeparamref</c> element.</param>
    /// <returns>The Markdown.</returns>
    private string Reference(XElement see)
    {
        if (!string.IsNullOrWhiteSpace(see.Value))
        {
            return MarkdownPage.Text(see.Value);
        }

        string target = (string?)see.Attribute("cref") ?? (string?)see.Attribute("name") ?? string.Empty;
        if (target.StartsWith("F:", StringComparison.Ordinal) && ConstantValue(target[2..]) is { } value)
        {
            return value;
        }

        string name = target.Contains(':', StringComparison.Ordinal) ? target[(target.IndexOf(':', StringComparison.Ordinal) + 1)..] : target;
        int parameters = name.IndexOf('(', StringComparison.Ordinal);
        if (parameters >= 0)
        {
            name = name[..parameters];
        }

        return MarkdownPage.Code(name[(name.LastIndexOf('.') + 1)..]);
    }

    /// <summary>The value of a constant of the assembly, in the invariant culture, or null when the field is not a constant.</summary>
    /// <param name="field">The field's full name, <c>Namespace.Type.Field</c>.</param>
    /// <returns>The value, or null.</returns>
    private string? ConstantValue(string field)
    {
        int dot = field.LastIndexOf('.');
        FieldInfo? info = _assembly.GetType(field[..dot])?.GetField(field[(dot + 1)..], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return info is { IsLiteral: true } ? Convert.ToString(info.GetRawConstantValue(), CultureInfo.InvariantCulture) : null;
    }

    /// <summary>Runs of whitespace, line ends included.</summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
