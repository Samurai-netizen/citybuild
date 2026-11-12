using CityBuilder.Api.Errors;
using CityBuilder.DocTools.Pages;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Simulation.Content;

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
