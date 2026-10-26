using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CityBuilder.Repository.Tests.Documentation;

/// <summary>
/// The rules of <c>core-instructions/14-coding-standards.md</c> → Documentation in code that a parser can check in one C# file: every
/// declaration has a doc comment with a description (an <c>&lt;inheritdoc/&gt;</c> only where there is something to inherit), every
/// <c>&lt;param&gt;</c> and <c>&lt;paramref/&gt;</c> names a real parameter, every primary constructor parameter is described, every local
/// function has a comment above it, and a file without a type starts with a comment that describes it.
/// </summary>
/// <remarks>
/// The rules read only the syntax tree, never a compiled assembly, so they apply alike to the server, the engine and the Unity scripts,
/// which only Unity compiles. They see whether a description is there, not whether it is good: that is for review.
/// </remarks>
internal static class DocumentationRules
{
    /// <summary>
    /// How every file is parsed: the newest language version, which also reads the C# 9 of the engine and the Unity scripts, and doc
    /// comments as structured trivia, so their XML elements can be inspected.
    /// </summary>
    private static readonly CSharpParseOptions _parseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.Latest)
        .WithDocumentationMode(DocumentationMode.Parse);

    /// <summary>Texts that stand in for a description without being one; compared after trimming and in lowercase.</summary>
    private static readonly HashSet<string> _placeholders = new(StringComparer.Ordinal) { "todo", "tbd", "...", "…", "-", "—" };

    /// <summary>Every documentation problem in one C# file, in source order.</summary>
    /// <param name="path">The file's path relative to the repository root, with <c>/</c> separators; only copied into each problem.</param>
    /// <param name="text">The file's content.</param>
    /// <returns>An empty list when the file and everything declared in it is documented.</returns>
    public static IReadOnlyList<DocumentationProblem> Check(string path, string text)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(text, _parseOptions).GetRoot();
        var problems = new List<DocumentationProblem>();
        foreach (SyntaxNode node in root.DescendantNodes())
        {
            if (node is LocalFunctionStatementSyntax localFunction)
            {
                CheckLocalFunction(path, localFunction, problems);
            }
            else if (node is MemberDeclarationSyntax member && IsDeclaration(member))
            {
                CheckDeclaration(path, member, problems);
            }
        }

        bool hasType = root.DescendantNodes().OfType<MemberDeclarationSyntax>().Any(member => member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);
        if (!hasType && !HasComment(root.GetLeadingTrivia()))
        {
            problems.Add(new DocumentationProblem(path, 1, "file", Path.GetFileName(path), "declares no type and does not start with a comment that says what it is for"));
        }

        return problems;
    }

    /// <summary>
    /// Whether a member declaration is one that must be documented: everything except the containers and fragments that hold no
    /// declaration of their own (namespaces, top-level statements, and the incomplete members a parser makes of broken code).
    /// </summary>
    private static bool IsDeclaration(MemberDeclarationSyntax member) =>
        member is not (BaseNamespaceDeclarationSyntax or GlobalStatementSyntax or IncompleteMemberSyntax);

    /// <summary>
    /// Adds the problems of one declaration: no doc comment, an empty or placeholder description, an <c>&lt;inheritdoc/&gt;</c> with
    /// nothing to inherit, a parameter tag that names no parameter, or an undescribed primary constructor parameter.
    /// </summary>
    private static void CheckDeclaration(string path, MemberDeclarationSyntax member, List<DocumentationProblem> problems)
    {
        (string kind, string name, SyntaxToken anchor) = Describe(member);
        int line = LineOf(anchor.IsKind(SyntaxKind.None) ? member.GetFirstToken() : anchor);
        DocumentationCommentTriviaSyntax? comment = DocCommentOf(member);
        if (comment is null)
        {
            problems.Add(new DocumentationProblem(path, line, kind, name, "has no doc comment (/// <summary>…</summary>)"));
            return;
        }

        if (HasElement(comment, "inheritdoc"))
        {
            if (!HasSomethingToInherit(member, comment))
            {
                problems.Add(new DocumentationProblem(path, line, kind, name,
                    "has an <inheritdoc/> but overrides or implements nothing: describe it, or name the source with <inheritdoc cref=\"…\"/>"));
            }
        }
        else
        {
            XmlElementSyntax? summary = ElementsOf(comment, "summary").FirstOrDefault();
            if (summary is null)
            {
                problems.Add(new DocumentationProblem(path, line, kind, name, "has a doc comment without a <summary>"));
            }
            else if (IsEmptyDescription(summary))
            {
                problems.Add(new DocumentationProblem(path, line, kind, name, "has an empty or placeholder <summary>"));
            }
        }

        HashSet<string> parameters = ParametersOf(member);
        foreach (string unknown in ParameterNamesIn(comment).Where(named => !parameters.Contains(named)).Distinct(StringComparer.Ordinal))
        {
            problems.Add(new DocumentationProblem(path, line, kind, name, $"names the parameter \"{unknown}\" in a <param> or <paramref/>, but has no parameter of that name"));
        }

        if (member is TypeDeclarationSyntax { ParameterList: { } parameterList })
        {
            var described = ElementsOf(comment, "param").Select(NameAttributeOf).ToHashSet(StringComparer.Ordinal);
            foreach (ParameterSyntax parameter in parameterList.Parameters.Where(parameter => !described.Contains(parameter.Identifier.Text)))
            {
                problems.Add(new DocumentationProblem(path, LineOf(parameter.Identifier), "parameter", parameter.Identifier.Text,
                    $"of the primary constructor of {name} has no <param name=\"{parameter.Identifier.Text}\"> in the type's doc comment"));
            }
        }
    }

    /// <summary>Adds a problem when a local function has no comment above it; XML doc comments are not allowed there (CS1587), so any comment counts.</summary>
    private static void CheckLocalFunction(string path, LocalFunctionStatementSyntax localFunction, List<DocumentationProblem> problems)
    {
        if (!HasComment(localFunction.GetLeadingTrivia()))
        {
            problems.Add(new DocumentationProblem(path, LineOf(localFunction.Identifier), "local function", localFunction.Identifier.Text,
                "has no comment above it (// …)"));
        }
    }

    /// <summary>
    /// Whether an <c>&lt;inheritdoc/&gt;</c> has a description to take: it names its source with <c>cref</c>, or the declaration overrides
    /// a member, implements an interface member explicitly, or is or belongs to a type with a base type or interface, whose members it
    /// may implement. Without any of these the compiler finds nothing to inherit and the declaration stays undescribed.
    /// </summary>
    private static bool HasSomethingToInherit(MemberDeclarationSyntax member, DocumentationCommentTriviaSyntax comment)
    {
        bool namesItsSource = comment.DescendantNodes().OfType<XmlCrefAttributeSyntax>().Any(cref =>
            cref.Parent is XmlEmptyElementSyntax { Name.LocalName.Text: "inheritdoc" } or XmlElementStartTagSyntax { Name.LocalName.Text: "inheritdoc" });
        bool overridesOrImplements = member.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || member is MethodDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
                or PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
                or EventDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
                or IndexerDeclarationSyntax { ExplicitInterfaceSpecifier: not null };
        BaseTypeDeclarationSyntax? type = member as BaseTypeDeclarationSyntax ?? member.Parent as BaseTypeDeclarationSyntax;
        return namesItsSource || overridesOrImplements || type?.BaseList is not null;
    }

    /// <summary>
    /// The parameter names a declaration's doc comment may use: its own parameters (a method's, constructor's, operator's, delegate's or
    /// indexer's, or a type's primary constructor's), and those of the primary constructor of the type it belongs to.
    /// </summary>
    private static HashSet<string> ParametersOf(MemberDeclarationSyntax member)
    {
        BaseParameterListSyntax? own = member switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList,
            DelegateDeclarationSyntax @delegate => @delegate.ParameterList,
            IndexerDeclarationSyntax indexer => indexer.ParameterList,
            TypeDeclarationSyntax type => type.ParameterList,
            _ => null,
        };
        ParameterListSyntax? primary = (member.Parent as TypeDeclarationSyntax)?.ParameterList;
        return new[] { own, primary }
            .SelectMany(list => list?.Parameters ?? [])
            .Select(parameter => parameter.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The parameter names a doc comment uses: the <c>name</c> of each top-level <c>&lt;param&gt;</c> and of each <c>&lt;paramref/&gt;</c> anywhere in it.</summary>
    private static IEnumerable<string> ParameterNamesIn(DocumentationCommentTriviaSyntax comment) =>
        ElementsOf(comment, "param").Select(NameAttributeOf)
            .Concat(comment.DescendantNodes().OfType<XmlEmptyElementSyntax>()
                .Where(element => element.Name.LocalName.Text == "paramref")
                .SelectMany(element => element.Attributes.OfType<XmlNameAttributeSyntax>())
                .Select(attribute => attribute.Identifier.Identifier.Text))
            .Where(parameterName => parameterName.Length > 0);

    /// <summary>The doc comment written right above a declaration (before its attributes), or null when there is none.</summary>
    private static DocumentationCommentTriviaSyntax? DocCommentOf(MemberDeclarationSyntax member) =>
        member.GetLeadingTrivia()
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .LastOrDefault();

    /// <summary>Whether a run of trivia holds any comment: <c>//</c>, <c>/* */</c> or a doc comment.</summary>
    private static bool HasComment(SyntaxTriviaList trivia) =>
        trivia.Any(item => item.IsKind(SyntaxKind.SingleLineCommentTrivia) || item.IsKind(SyntaxKind.MultiLineCommentTrivia)
            || item.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || item.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

    /// <summary>The top-level XML elements with a start and an end tag of a given name, such as every <c>&lt;param&gt;</c> of a comment.</summary>
    private static IEnumerable<XmlElementSyntax> ElementsOf(DocumentationCommentTriviaSyntax comment, string name) =>
        comment.Content.OfType<XmlElementSyntax>().Where(element => element.StartTag.Name.LocalName.Text == name);

    /// <summary>Whether a comment holds an element of a given name anywhere, written either as <c>&lt;name/&gt;</c> or as <c>&lt;name&gt;…&lt;/name&gt;</c>.</summary>
    private static bool HasElement(DocumentationCommentTriviaSyntax comment, string name) =>
        comment.DescendantNodes().Any(node =>
            (node is XmlEmptyElementSyntax empty && empty.Name.LocalName.Text == name)
            || (node is XmlElementSyntax element && element.StartTag.Name.LocalName.Text == name));

    /// <summary>
    /// Whether a <c>&lt;summary&gt;</c> says nothing: no text and no nested element (a lone <c>&lt;see cref="…"/&gt;</c> counts as
    /// content), or only a placeholder such as "TODO".
    /// </summary>
    private static bool IsEmptyDescription(XmlElementSyntax summary)
    {
        string text = string.Concat(summary.Content.OfType<XmlTextSyntax>().SelectMany(part => part.TextTokens).Select(token => token.Text)).Trim();
        bool hasNestedElement = summary.Content.Any(part => part is XmlElementSyntax or XmlEmptyElementSyntax);
        return (text.Length == 0 && !hasNestedElement) || _placeholders.Contains(text.ToLowerInvariant());
    }

    /// <summary>The value of a <c>&lt;param&gt;</c> element's <c>name</c> attribute, or an empty text when it has none.</summary>
    private static string NameAttributeOf(XmlElementSyntax element) =>
        element.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().Select(attribute => attribute.Identifier.Identifier.Text).FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// What a declaration is called in a problem: its kind (<c>method</c>, <c>field</c>, …), its name, and the token whose line is
    /// reported (the name, so the line is right even below a block of attributes; <see cref="SyntaxKind.None"/> when there is none).
    /// </summary>
    private static (string Kind, string Name, SyntaxToken Anchor) Describe(MemberDeclarationSyntax member) => member switch
    {
        ClassDeclarationSyntax type => ("class", type.Identifier.Text, type.Identifier),
        StructDeclarationSyntax type => ("struct", type.Identifier.Text, type.Identifier),
        InterfaceDeclarationSyntax type => ("interface", type.Identifier.Text, type.Identifier),
        RecordDeclarationSyntax type => ("record", type.Identifier.Text, type.Identifier),
        EnumDeclarationSyntax type => ("enum", type.Identifier.Text, type.Identifier),
        DelegateDeclarationSyntax type => ("delegate", type.Identifier.Text, type.Identifier),
        EnumMemberDeclarationSyntax value => ("enum member", value.Identifier.Text, value.Identifier),
        MethodDeclarationSyntax method => ("method", method.Identifier.Text, method.Identifier),
        ConstructorDeclarationSyntax constructor => ("constructor", constructor.Identifier.Text, constructor.Identifier),
        DestructorDeclarationSyntax destructor => ("finalizer", "~" + destructor.Identifier.Text, destructor.Identifier),
        OperatorDeclarationSyntax op => ("operator", "operator " + op.OperatorToken.Text, op.OperatorToken),
        ConversionOperatorDeclarationSyntax conversion => ("operator", $"{conversion.ImplicitOrExplicitKeyword.Text} operator {conversion.Type}", conversion.ImplicitOrExplicitKeyword),
        PropertyDeclarationSyntax property => ("property", property.Identifier.Text, property.Identifier),
        IndexerDeclarationSyntax indexer => ("indexer", "this[]", indexer.ThisKeyword),
        EventDeclarationSyntax eventDeclaration => ("event", eventDeclaration.Identifier.Text, eventDeclaration.Identifier),
        EventFieldDeclarationSyntax eventField => ("event", NamesOf(eventField.Declaration), eventField.Declaration.Variables[0].Identifier),
        FieldDeclarationSyntax field => (field.Modifiers.Any(SyntaxKind.ConstKeyword) ? "constant" : "field", NamesOf(field.Declaration), field.Declaration.Variables[0].Identifier),
        _ => (member.Kind().ToString(), string.Empty, default),
    };

    /// <summary>The names a field or event declaration declares, joined by commas (<c>int _first, _second;</c> → <c>_first, _second</c>).</summary>
    private static string NamesOf(VariableDeclarationSyntax declaration) =>
        string.Join(", ", declaration.Variables.Select(variable => variable.Identifier.Text));

    /// <summary>The 1-based line of a token, as editors and compiler messages count lines.</summary>
    private static int LineOf(SyntaxToken token) => token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
