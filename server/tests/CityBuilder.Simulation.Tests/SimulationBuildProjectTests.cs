using System.Xml.Linq;

namespace CityBuilder.Simulation.Tests;

/// <summary>
/// Guards the settings of the .NET build project that the compiled assembly does not show: the language version Unity can compile,
/// the strict build, the compiled folder, and the absence of references. Without them a loosened project file would still build and
/// pass every other test. The values are read from the project file; the target <c>GuardUnityCompatibleBuild</c> in that file checks
/// the evaluated values during the build, which also catches an override from a Directory.Build.targets file or the command line.
/// </summary>
public sealed class SimulationBuildProjectTests
{
    private static readonly XDocument _project =
        XDocument.Load(Path.Combine(RepositoryPaths.Root, "shared", "Simulation.Build", "CityBuilder.Simulation.csproj"));

    [Fact]
    public void BuildProject_LangVersion_IsCSharp9()
    {
        // Unity 6.3 compiles the same sources as C# 9. A newer language version here would let C# 10 syntax pass this build and break Unity.
        Assert.Equal("9.0", PropertyOf("LangVersion"));
    }

    [Fact]
    public void BuildProject_TreatWarningsAsErrors_IsTrue()
    {
        Assert.Equal("true", PropertyOf("TreatWarningsAsErrors"));
    }

    [Fact]
    public void BuildProject_GenerateDocumentationFile_IsTrue()
    {
        // Together with warnings as errors, this makes a missing XML comment (CS1591) fail the build.
        Assert.Equal("true", PropertyOf("GenerateDocumentationFile"));
    }

    [Fact]
    public void BuildProject_AnalysisSettings_MatchTheServerConventions()
    {
        // The project is outside server/, so it repeats the settings of server/Directory.Build.props (core-instructions/14, decision 021).
        Assert.Equal("latest-recommended", PropertyOf("AnalysisLevel"));
        Assert.Equal("true", PropertyOf("EnforceCodeStyleInBuild"));
    }

    [Fact]
    public void BuildProject_WarningSuppressions_AreAbsent()
    {
        // A NoWarn would defeat "warnings as errors", for example CS1591 (a missing XML comment).
        Assert.Empty(_project.Descendants("NoWarn"));
        Assert.Empty(_project.Descendants("WarningsNotAsErrors"));
    }

    [Fact]
    public void BuildProject_CompileItems_AreTheRuntimeFolderOnly()
    {
        // Default globbing off, and exactly one explicit glob: the .NET build compiles the same files that Unity compiles.
        XElement[] compileItems = _project.Descendants("Compile").ToArray();

        Assert.Equal("false", PropertyOf("EnableDefaultCompileItems"));
        Assert.Equal(["../Simulation/Runtime/**/*.cs"], compileItems.Select(item => (string?)item.Attribute("Include")));
        Assert.All(compileItems, item => Assert.Null(item.Attribute("Remove")));
    }

    [Fact]
    public void BuildProject_References_AreAbsent()
    {
        // The engine references only the base class library: no package (a package needs G-Dependency), no assembly file, no other
        // project, and no framework reference.
        Assert.Empty(_project.Descendants("PackageReference"));
        Assert.Empty(_project.Descendants("Reference"));
        Assert.Empty(_project.Descendants("ProjectReference"));
        Assert.Empty(_project.Descendants("FrameworkReference"));
    }

    [Fact]
    public void BuildProject_UnityCompatibilityGuard_Exists()
    {
        Assert.Single(_project.Descendants("Target"), target => (string?)target.Attribute("Name") == "GuardUnityCompatibleBuild");
    }

    private static string? PropertyOf(string name) =>
        _project.Root?.Elements("PropertyGroup").Elements(name).Select(element => element.Value.Trim()).SingleOrDefault();
}
