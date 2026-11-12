using CityBuilder.Api.Errors;
using CityBuilder.DocTools.Pages;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Simulation.Content;
using Microsoft.EntityFrameworkCore;

namespace CityBuilder.DocTools.Tests;

/// <summary>Each generated page shows everything its source defines, so nothing new is left off the reference.</summary>
public sealed class PagesTests
{
    /// <summary>The content the server loads by default, as the content page reads it.</summary>
    private static readonly GameContent _content =
        ContentFileLoader.Load(Path.Combine(TestRepository.Root, "server", "content", "mvp-content.json"));

    /// <summary>The content page has a section for every building type and a row for every item and research node of the content.</summary>
    [Fact]
    public void ContentPage_EveryBuildingItemAndResearch_IsListed()
    {
        string text = ContentPage.Generate(TestRepository.Root).Text;

        Assert.All(_content.Buildings, building => Assert.Contains($"#### {building.Name} ({building.Id.Value})", text, StringComparison.Ordinal));
        Assert.All(_content.Items, item => Assert.Contains($"| `{item.Id.Value}` | {item.Name} |", text, StringComparison.Ordinal));
        Assert.All(_content.Research, node => Assert.Contains($"| `{node.Id.Value}` | {node.Name} |", text, StringComparison.Ordinal));
    }

    /// <summary>The errors page has one row per catalogue code, with its HTTP status.</summary>
    [Fact]
    public void ErrorsPage_EveryCatalogueCode_HasARowWithItsStatus()
    {
        string text = ErrorsPage.Generate().Text;

        Assert.All(ErrorCatalogue.All, error => Assert.Contains($"| `{error.Code}` | {error.Status} |", text, StringComparison.Ordinal));
    }

    /// <summary>The project map names every .NET project of the server's solution.</summary>
    [Fact]
    public void ProjectMapPage_EveryProjectOfTheSolution_IsListed()
    {
        string text = ProjectMapPage.Generate(TestRepository.Root).Text;
        string solution = File.ReadAllText(Path.Combine(TestRepository.Root, "server", "CityBuilder.slnx"));
        IEnumerable<string> projects = System.Text.RegularExpressions.Regex.Matches(solution, @"([A-Za-z.]+)\.csproj")
            .Select(match => match.Groups[1].Value);

        Assert.All(projects, project => Assert.Contains($"| `{project}` |", text, StringComparison.Ordinal));
    }

    /// <summary>The schema page has a section for every table of the EF Core model.</summary>
    [Fact]
    public void DbSchemaPage_EveryTableOfTheModel_HasASection()
    {
        string text = DbSchemaPage.Generate().Text;
        using CityBuilderDbContext context = new CityBuilderDbContextDesignTimeFactory().CreateDbContext([]);

        Assert.All(context.Model.GetEntityTypes().Select(entity => entity.GetTableName()),
            table => Assert.Contains($"\n## {table}\n", text, StringComparison.Ordinal));
    }

    /// <summary>
    /// The configuration page has the keys the server reads: a plain one, the connection string under <c>ConnectionStrings:</c>, and a nested
    /// rate limit. A reference to a constant in a doc comment shows the constant's value.
    /// </summary>
    /// <param name="key">A key the page must have.</param>
    [Theory]
    [InlineData("Auth:SigningKey")]
    [InlineData("ConnectionStrings:CityBuilder")]
    [InlineData("RateLimits:Commands:Burst:Limit")]
    [InlineData("Simulation:PresenceGraceSeconds")]
    public void ConfigurationPage_ServerKeys_AreListed(string key) =>
        Assert.Contains($"| `{key}` |", ConfigurationPage.Generate(TestRepository.Root).Text, StringComparison.Ordinal);

    /// <summary>A doc comment's reference to a constant shows the value: the lock timeout's range reads "between 1 and 60000".</summary>
    [Fact]
    public void ConfigurationPage_ConstantReferences_ShowTheirValues() =>
        Assert.Contains("between 1 and 60000", ConfigurationPage.Generate(TestRepository.Root).Text, StringComparison.Ordinal);

    /// <summary>
    /// The endpoints page has a row for every operation of the build-time OpenAPI document, with its auth: the game endpoints need the bearer
    /// token, the dev login doesn't.
    /// </summary>
    /// <param name="row">The start of an operation's row.</param>
    [Theory]
    [InlineData("| GET | `/api/v1/city` | Bearer |")]
    [InlineData("| GET | `/api/v1/content` | Bearer |")]
    [InlineData("| POST | `/api/v1/commands/place-building` | Bearer |")]
    [InlineData("| POST | `/api/v1/auth/dev-login` | none |")]
    public void EndpointsPage_Operations_AreListedWithTheirAuth(string row) =>
        Assert.Contains(row, EndpointsPage.Generate(TestRepository.Root).Text, StringComparison.Ordinal);

    /// <summary>Every schema a row or a property links to has its own section, so no link on the page is broken.</summary>
    [Fact]
    public void EndpointsPage_SchemaLinks_PointToSections()
    {
        string text = EndpointsPage.Generate(TestRepository.Root).Text;

        IEnumerable<string> targets = System.Text.RegularExpressions.Regex.Matches(text, @"\]\(#([a-z0-9]+)\)").Select(match => match.Groups[1].Value);
        IEnumerable<string> sections = System.Text.RegularExpressions.Regex.Matches(text, @"^### (\S+)$", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value.ToLowerInvariant());

        Assert.Empty(targets.Except(sections));
    }

    /// <summary>Thousandths are written with at most three decimals and no trailing zeros.</summary>
    /// <param name="milli">The value in thousandths.</param>
    /// <param name="expected">The text.</param>
    [Theory]
    [InlineData(0, "0")]
    [InlineData(60_000, "60")]
    [InlineData(7_500, "7.5")]
    [InlineData(333, "0.333")]
    [InlineData(1_050, "1.05")]
    public void Milli_Values_AreWrittenWithoutTrailingZeros(long milli, string expected) => Assert.Equal(expected, PageFormat.Milli(milli));

    /// <summary>Durations show the exact seconds, and a readable form from one minute up.</summary>
    /// <param name="seconds">The duration.</param>
    /// <param name="expected">The text.</param>
    [Theory]
    [InlineData(45, "45 s")]
    [InlineData(90, "90 s (1 min 30 s)")]
    [InlineData(5_400, "5400 s (1 h 30 min)")]
    [InlineData(90_000, "90000 s (1 d 1 h)")]
    public void Duration_Values_ShowExactAndReadable(long seconds, string expected) => Assert.Equal(expected, PageFormat.Duration(seconds));

    /// <summary>Pipes and angle brackets in text are escaped for a table cell and the handbook.</summary>
    [Fact]
    public void Text_PipeAndAngleBrackets_AreEscaped() => Assert.Equal("a \\| &lt;b&gt;", MarkdownPage.Text("a | <b>"));
}
