using System.Xml.Linq;

namespace CityBuilder.Simulation.Tests;

/// <summary>
/// Guards the settings of the .NET build project that the compiled assembly does not show: the language version Unity can compile,
/// the strict build, and the absence of packages. Without them a loosened project file would still build and pass every other test.
/// </summary>
public sealed class SimulationBuildProjectTests
{
    private static readonly XDocument _project =
        XDocument.Load(Path.Combine(RepositoryPaths.Root, "shared", "Simulation.Build", "CityBuilder.Simulation.csproj"));

    [Fact]
    public void LangVersion_IsCSharp9()
    {
        // Unity 6.3 compiles the same sources as C# 9. A newer language version here would let C# 10 syntax pass this build and break Unity.
        Assert.Equal("9.0", PropertyOf("LangVersion"));
    }

    [Fact]
    public void Build_TreatsWarningsAsErrors()
    {
        Assert.Equal("true", PropertyOf("TreatWarningsAsErrors"));
    }

    [Fact]
    public void Build_WritesTheXmlDocumentationFile()
    {
        // Together with warnings as errors, this makes a missing XML comment (CS1591) fail the build.
        Assert.Equal("true", PropertyOf("GenerateDocumentationFile"));
    }

    [Fact]
    public void Project_HasNoPackageReferences()
    {
        // The engine references only the base class library; a package needs G-Dependency and a place in the dependency table.
        Assert.Empty(_project.Descendants("PackageReference"));
    }

    private static string? PropertyOf(string name) =>
        _project.Root?.Elements("PropertyGroup").Elements(name).Select(element => element.Value.Trim()).SingleOrDefault();
}
