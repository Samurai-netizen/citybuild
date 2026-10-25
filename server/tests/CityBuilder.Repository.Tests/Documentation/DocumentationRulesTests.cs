namespace CityBuilder.Repository.Tests.Documentation;

/// <summary>
/// The rules of <see cref="DocumentationRules"/> on small C# samples, one behaviour per test: what counts as documented, and that every
/// kind of undocumented declaration, private ones included, is reported with its kind, name and line.
/// </summary>
public sealed class DocumentationRulesTests
{
    /// <summary>The path every sample is checked under; it only appears in the problems.</summary>
    private const string _samplePath = "samples/Sample.cs";

    /// <summary>A file with one documented declaration of every kind, private ones and a commented local function included, has no problem.</summary>
    [Fact]
    public void Check_EveryKindOfDeclarationDocumented_ReportsNothing()
    {
        const string sample = """
            namespace Samples;

            /// <summary>A class.</summary>
            public sealed class Counter
            {
                /// <summary>A constant.</summary>
                public const int Limit = 10;

                /// <summary>A private field.</summary>
                private int _count;

                /// <summary>An event field.</summary>
                public event System.Action? Changed;

                /// <summary>A constructor.</summary>
                public Counter() { }

                /// <summary>A finalizer.</summary>
                ~Counter() { }

                /// <summary>A property.</summary>
                public int Count => _count;

                /// <summary>An indexer.</summary>
                public int this[int index] => index;

                /// <summary>An event with accessors.</summary>
                public event System.Action Reset { add { } remove { } }

                /// <summary>A method with a local function.</summary>
                public void Add()
                {
                    // The local function's comment.
                    int Next() => _count + 1;
                    _count = Next();
                }

                /// <summary>An operator.</summary>
                public static Counter operator +(Counter left, Counter right) => left;

                /// <summary>A conversion operator.</summary>
                public static implicit operator int(Counter counter) => counter._count;

                /// <summary>A nested type.</summary>
                private enum Mode
                {
                    /// <summary>An enum member.</summary>
                    Off,
                }
            }

            /// <summary>A struct.</summary>
            public struct Point { }

            /// <summary>An interface.</summary>
            public interface IShape { }

            /// <summary>A record whose parameters are described.</summary>
            /// <param name="Width">The width.</param>
            public sealed record Size(int Width);

            /// <summary>A delegate.</summary>
            public delegate void Handler();
            """;

        Assert.Empty(DocumentationRules.Check(_samplePath, sample));
    }

    /// <summary>Each declaration without a doc comment is one problem with its kind, name and the line of its name, private ones too.</summary>
    [Fact]
    public void Check_UndocumentedDeclarations_ReportsEachWithKindNameAndLine()
    {
        const string sample = """
            namespace Samples;

            /// <summary>A documented class; everything inside it is not.</summary>
            public sealed class Counter
            {
                private const int Limit = 10;
                private int _count;
                internal event System.Action? Changed;
                private Counter() { }
                private int Count => _count;
                private int this[int index] => index;
                private void Add() { }
                public static Counter operator +(Counter left, Counter right) => left;
                public static explicit operator int(Counter counter) => counter._count;
                private enum Mode
                {
                    Off,
                }
            }

            internal struct Point { }
            """;

        string[] problems = DocumentationRules.Check(_samplePath, sample).Select(problem => $"{problem.Line} {problem.Kind} {problem.Name}").ToArray();

        Assert.Equal(
            [
                "6 constant Limit", "7 field _count", "8 event Changed", "9 constructor Counter", "10 property Count", "11 indexer this[]",
                "12 method Add", "13 operator operator +", "14 operator explicit operator int", "15 enum Mode", "17 enum member Off", "21 struct Point",
            ],
            problems);
    }

    /// <summary>A field declaration that declares several names is one declaration and one problem, naming all of them.</summary>
    [Fact]
    public void Check_FieldWithSeveralNames_ReportsOneProblemNamingAll()
    {
        const string sample = """
            /// <summary>A class.</summary>
            internal sealed class Pair
            {
                private int _first, _second;
            }
            """;

        DocumentationProblem problem = Assert.Single(DocumentationRules.Check(_samplePath, sample));
        Assert.Equal(("field", "_first, _second"), (problem.Kind, problem.Name));
    }

    /// <summary>An <c>&lt;inheritdoc/&gt;</c> stands for the description of the member it overrides or implements.</summary>
    [Fact]
    public void Check_InheritDoc_CountsAsADescription()
    {
        const string sample = """
            /// <summary>A class.</summary>
            internal sealed class Named
            {
                /// <inheritdoc/>
                public override string ToString() => "named";
            }
            """;

        Assert.Empty(DocumentationRules.Check(_samplePath, sample));
    }

    /// <summary>A summary that holds only a <c>&lt;see cref&gt;</c> says something (the name of what it refers to), so it counts.</summary>
    [Fact]
    public void Check_SummaryWithOnlyAReference_CountsAsADescription()
    {
        const string sample = """
            /// <summary><see cref="System.String"/></summary>
            internal sealed class Wrapper { }
            """;

        Assert.Empty(DocumentationRules.Check(_samplePath, sample));
    }

    /// <summary>An empty summary, or one with only a placeholder such as "TODO", is reported as no description at all.</summary>
    [Theory]
    [InlineData("/// <summary></summary>")]
    [InlineData("/// <summary>   </summary>")]
    [InlineData("/// <summary>TODO</summary>")]
    [InlineData("/// <summary> … </summary>")]
    public void Check_EmptyOrPlaceholderSummary_ReportsIt(string comment)
    {
        string sample = comment + "\ninternal sealed class Empty { }\n";

        DocumentationProblem problem = Assert.Single(DocumentationRules.Check(_samplePath, sample));
        Assert.Equal("has an empty or placeholder <summary>", problem.Message);
    }

    /// <summary>A doc comment without a <c>&lt;summary&gt;</c> (only remarks, say) lacks the one element every declaration needs.</summary>
    [Fact]
    public void Check_DocCommentWithoutSummary_ReportsIt()
    {
        const string sample = """
            /// <remarks>Only remarks.</remarks>
            internal sealed class Remarked { }
            """;

        DocumentationProblem problem = Assert.Single(DocumentationRules.Check(_samplePath, sample));
        Assert.Equal("has a doc comment without a <summary>", problem.Message);
    }

    /// <summary>
    /// A doc comment written below the attributes doesn't belong to the declaration (the compiler ignores it too, CS1587), so the
    /// declaration counts as undocumented.
    /// </summary>
    [Fact]
    public void Check_DocCommentBelowTheAttributes_ReportsAMissingDocComment()
    {
        const string sample = """
            /// <summary>A class.</summary>
            internal sealed class Tests
            {
                [System.Obsolete]
                /// <summary>Misplaced.</summary>
                public void Old() { }
            }
            """;

        DocumentationProblem problem = Assert.Single(DocumentationRules.Check(_samplePath, sample));
        Assert.Equal(("method", "Old", 6), (problem.Kind, problem.Name, problem.Line));
    }

    /// <summary>A local function without a comment above it is reported; doc comments are not allowed there, so a plain comment is asked for.</summary>
    [Fact]
    public void Check_LocalFunctionWithoutComment_ReportsIt()
    {
        const string sample = """
            /// <summary>A class.</summary>
            internal sealed class Calculator
            {
                /// <summary>A method.</summary>
                public int Twice(int value)
                {
                    int Double(int x) => x * 2;
                    return Double(value);
                }
            }
            """;

        DocumentationProblem problem = Assert.Single(DocumentationRules.Check(_samplePath, sample));
        Assert.Equal(("local function", "Double", 7), (problem.Kind, problem.Name, problem.Line));
    }

    /// <summary>Every primary constructor parameter needs a <c>&lt;param&gt;</c> in the type's doc comment, since the parameter is part of the type.</summary>
    [Fact]
    public void Check_PrimaryConstructorParameterWithoutParamTag_ReportsIt()
    {
        const string sample = """
            /// <summary>A service.</summary>
            /// <param name="clock">The clock.</param>
            internal sealed class Service(System.TimeProvider clock, int retries)
            {
            }
            """;

        DocumentationProblem problem = Assert.Single(DocumentationRules.Check(_samplePath, sample));
        Assert.Equal(("parameter", "retries", 3), (problem.Kind, problem.Name, problem.Line));
    }

    /// <summary>A file that declares no type (top-level statements, say) must start with a comment that says what it is for.</summary>
    [Fact]
    public void Check_FileWithoutTypeOrLeadingComment_ReportsTheFile()
    {
        const string sample = """
            using System;

            Console.WriteLine("hello");
            """;

        DocumentationProblem problem = Assert.Single(DocumentationRules.Check(_samplePath, sample));
        Assert.Equal(("file", "Sample.cs", 1), (problem.Kind, problem.Name, problem.Line));
    }

    /// <summary>A file of top-level statements that starts with a comment, after <c>#nullable</c> or not, describes itself.</summary>
    [Fact]
    public void Check_TopLevelStatementsWithLeadingComment_ReportsNothing()
    {
        const string sample = """
            #nullable enable
            // The program's entry point: prints a greeting.
            using System;

            Console.WriteLine("hello");
            """;

        Assert.Empty(DocumentationRules.Check(_samplePath, sample));
    }

    /// <summary>A problem prints as <c>path:line: kind name message</c>, the form editors and CI logs turn into a link.</summary>
    [Fact]
    public void ToString_Problem_IsPathLineKindNameMessage()
    {
        var problem = new DocumentationProblem("server/src/A.cs", 12, "method", "Run", "has no doc comment");

        Assert.Equal("server/src/A.cs:12: method Run has no doc comment", problem.ToString());
    }
}
