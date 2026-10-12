using System.Text.Json.Nodes;

namespace CityBuilder.Simulation.Tests;

/// <summary>
/// Guards what only Unity sees of the shared package (the asmdef and package.json) and the layout rules of
/// core-instructions/03-architecture.md. The .NET build cannot check these.
/// </summary>
public sealed class SimulationPackageTests
{
    private static readonly string _packageFolder = Path.Combine(RepositoryPaths.Root, "shared", "Simulation");
    private static readonly string _runtimeFolder = Path.Combine(_packageFolder, "Runtime");

    // ----- Runtime/CityBuilder.Simulation.asmdef -----

    [Fact]
    public void Asmdef_KeepsNoEngineReferences()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.True(BoolOf(asmdef, "noEngineReferences") == true);
    }

    [Fact]
    public void Asmdef_ReferencesNoOtherAssemblyAndNoPrecompiledLibrary()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.Empty(ArrayOf(asmdef, "references"));
        Assert.Empty(ArrayOf(asmdef, "precompiledReferences"));
        Assert.False(BoolOf(asmdef, "overrideReferences") ?? false);
    }

    [Fact]
    public void Asmdef_IsNamedLikeTheDotNetAssemblyAndRootNamespace()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.Equal(typeof(ItemId).Assembly.GetName().Name, (string?)asmdef["name"]);
        Assert.Equal("CityBuilder.Simulation", (string?)asmdef["rootNamespace"]);
    }

    [Fact]
    public void Asmdef_IsAutoReferenced()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.True(BoolOf(asmdef, "autoReferenced") == true);
    }

    // ----- package.json -----

    [Fact]
    public void PackageJson_NamesThePackage()
    {
        Assert.Equal("com.citybuilder.simulation", (string?)ReadPackageJson()["name"]);
    }

    [Fact]
    public void PackageJson_RequiresUnity63()
    {
        Assert.Equal("6000.3", (string?)ReadPackageJson()["unity"]);
    }

    [Fact]
    public void PackageJson_HasASemanticVersion()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", (string?)ReadPackageJson()["version"] ?? string.Empty);
    }

    // ----- layout -----

    [Fact]
    public void PackageFolder_ContainsNoBinOrObjFolders()
    {
        // Unity imports every file of a package folder, so build output must stay in shared/Simulation.Build.
        string[] buildOutput = Directory.EnumerateDirectories(_packageFolder, "*", SearchOption.AllDirectories)
            .Where(path => IsNamed(path, "bin") || IsNamed(path, "obj"))
            .ToArray();

        Assert.Empty(buildOutput);
    }

    [Fact]
    public void RuntimeSources_AreFound()
    {
        // Keeps the check below from passing on an empty list (a wrong path would find nothing).
        Assert.NotEmpty(RuntimeSources());
    }

    [Fact]
    public void RuntimeSources_StartWithNullableEnable()
    {
        // Unity does not read the csproj, so the directive in each file is the only thing that turns nullable annotations on there.
        string[] withoutDirective = RuntimeSources()
            .Where(path => File.ReadLines(path).FirstOrDefault() != "#nullable enable")
            .Select(path => Path.GetRelativePath(_runtimeFolder, path))
            .ToArray();

        Assert.Empty(withoutDirective);
    }

    // ----- helpers -----

    private static JsonObject ReadAsmdef() => ReadJsonObject(Path.Combine(_runtimeFolder, "CityBuilder.Simulation.asmdef"));

    private static JsonObject ReadPackageJson() => ReadJsonObject(Path.Combine(_packageFolder, "package.json"));

    private static JsonObject ReadJsonObject(string path)
    {
        JsonNode? root = JsonNode.Parse(File.ReadAllText(path));

        Assert.NotNull(root);
        return root.AsObject();
    }

    private static bool? BoolOf(JsonObject json, string propertyName) => (bool?)json[propertyName];

    // Unity treats a missing array as an empty one.
    private static JsonArray ArrayOf(JsonObject json, string propertyName) => json[propertyName]?.AsArray() ?? [];

    private static string[] RuntimeSources() => Directory.GetFiles(_runtimeFolder, "*.cs", SearchOption.AllDirectories);

    private static bool IsNamed(string path, string name) => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase);
}
