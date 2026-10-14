using System.Text.Json.Nodes;

namespace CityBuilder.Simulation.Tests;

/// <summary>
/// Guards what only Unity sees of the shared package (the asmdef and package.json) and the layout rules of
/// core-instructions/03-architecture.md. The .NET build cannot check these.
/// </summary>
public sealed class SimulationPackageTests
{
    // What a Unity package of sources may hold. Anything else (a dll, a pdb, a restore cache, a project file) is build output or clutter.
    private static readonly string[] _allowedExtensions = [".cs", ".asmdef", ".meta", ".md"];

    private static readonly string _packageFolder = Path.Combine(RepositoryPaths.Root, "shared", "Simulation");
    private static readonly string _runtimeFolder = Path.Combine(_packageFolder, "Runtime");

    // ----- Runtime/CityBuilder.Simulation.asmdef -----

    [Fact]
    public void Asmdef_NoEngineReferences_IsTrue()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.True(BoolOf(asmdef, "noEngineReferences") == true);
    }

    [Fact]
    public void Asmdef_AssemblyAndLibraryReferences_AreAbsent()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.Empty(ArrayOf(asmdef, "references"));
        Assert.Empty(ArrayOf(asmdef, "precompiledReferences"));
        Assert.False(BoolOf(asmdef, "overrideReferences") ?? false);
    }

    [Fact]
    public void Asmdef_PlatformAndDefineRestrictions_AreAbsent()
    {
        // The engine must compile into every Unity build target. A platform filter or a define constraint would drop it from some or all
        // of them while the .NET build, which ignores the asmdef, stays green.
        JsonObject asmdef = ReadAsmdef();

        Assert.Empty(ArrayOf(asmdef, "includePlatforms"));
        Assert.Empty(ArrayOf(asmdef, "excludePlatforms"));
        Assert.Empty(ArrayOf(asmdef, "defineConstraints"));
        Assert.Empty(ArrayOf(asmdef, "versionDefines"));
    }

    [Fact]
    public void Asmdef_Name_MatchesTheDotNetAssemblyAndRootNamespace()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.Equal(typeof(ItemId).Assembly.GetName().Name, (string?)asmdef["name"]);
        Assert.Equal("CityBuilder.Simulation", (string?)asmdef["rootNamespace"]);
    }

    [Fact]
    public void Asmdef_AutoReferenced_IsTrue()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.True(BoolOf(asmdef, "autoReferenced") == true);
    }

    [Fact]
    public void PackageFolder_AssemblyDefinitions_AreTheRuntimeAsmdefOnly()
    {
        // A second asmdef (for example under Runtime/Rules/) would split the engine into two Unity assemblies, and an asmref would add
        // sources to another one, while the .NET project still compiles one assembly from all of them.
        string[] asmdefs = Directory.GetFiles(_packageFolder, "*.asmdef", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_packageFolder, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["Runtime/CityBuilder.Simulation.asmdef"], asmdefs);
        Assert.Empty(Directory.GetFiles(_packageFolder, "*.asmref", SearchOption.AllDirectories));
    }

    // ----- package.json -----

    [Fact]
    public void PackageJson_Name_IsTheCityBuilderSimulationPackage()
    {
        Assert.Equal("com.citybuilder.simulation", (string?)ReadPackageJson()["name"]);
    }

    [Fact]
    public void PackageJson_UnityField_RequiresUnity63()
    {
        Assert.Equal("6000.3", (string?)ReadPackageJson()["unity"]);
    }

    [Fact]
    public void PackageJson_Version_IsSemantic()
    {
        // ASCII digits and \z on purpose: \d matches any Unicode digit and $ also matches before a final newline.
        Assert.Matches(@"^[0-9]+\.[0-9]+\.[0-9]+\z", (string?)ReadPackageJson()["version"] ?? string.Empty);
    }

    // ----- layout -----

    [Fact]
    public void PackageFolder_BuildOutputFolders_AreAbsent()
    {
        // Unity imports every file of a package folder, so build output must stay in shared/Simulation.Build.
        string[] buildOutput = Directory.EnumerateDirectories(_packageFolder, "*", SearchOption.AllDirectories)
            .Where(path => IsNamed(path, "bin") || IsNamed(path, "obj"))
            .ToArray();

        Assert.Empty(buildOutput);
    }

    [Fact]
    public void PackageFolder_Files_AreSourcesAndPackageMetadataOnly()
    {
        string[] unexpected = Directory.EnumerateFiles(_packageFolder, "*", SearchOption.AllDirectories)
            .Where(path => !IsPackageFile(path))
            .Select(path => Path.GetRelativePath(_packageFolder, path))
            .ToArray();

        Assert.Empty(unexpected);
    }

    [Fact]
    public void PackageFolder_EveryFileAndFolder_HasItsMetaFile()
    {
        // The .meta files of this mutable local package are committed (M0-04). Without one, Unity creates it with a new GUID on every
        // machine that opens the project, and the asset's identity differs between clones.
        string[] withoutMeta = PackageEntries()
            .Where(path => !path.EndsWith(".meta", StringComparison.Ordinal) && !File.Exists(path + ".meta"))
            .Select(path => Path.GetRelativePath(_packageFolder, path))
            .ToArray();

        Assert.Empty(withoutMeta);
    }

    [Fact]
    public void PackageFolder_EveryMetaFile_BelongsToAFileOrFolder()
    {
        string[] orphans = Directory.EnumerateFiles(_packageFolder, "*.meta", SearchOption.AllDirectories)
            .Where(meta => !File.Exists(meta[..^".meta".Length]) && !Directory.Exists(meta[..^".meta".Length]))
            .Select(path => Path.GetRelativePath(_packageFolder, path))
            .ToArray();

        Assert.Empty(orphans);
    }

    [Fact]
    public void PackageFolder_MetaFileGuids_AreUniqueAndWellFormed()
    {
        // Unity identifies an asset by the GUID in its .meta file; a copied .meta file would give two assets the same identity.
        string[] guids = Directory.EnumerateFiles(_packageFolder, "*.meta", SearchOption.AllDirectories)
            .Select(meta => File.ReadLines(meta).FirstOrDefault(line => line.StartsWith("guid: ", StringComparison.Ordinal)) ?? meta)
            .Select(line => line["guid: ".Length..])
            .ToArray();

        Assert.NotEmpty(guids);
        Assert.All(guids, guid => Assert.Matches("^[0-9a-f]{32}\\z", guid));
        Assert.Equal(guids.Length, guids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RuntimeSources_Folder_ContainsSourceFiles()
    {
        // Keeps the check below from passing on an empty list (a wrong path would find nothing).
        Assert.NotEmpty(RuntimeSources());
    }

    [Fact]
    public void RuntimeSources_FirstLine_IsNullableEnable()
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

    // Every file and folder below the package root. The root itself has no .meta file: it is the package, not an asset in it.
    private static IEnumerable<string> PackageEntries() =>
        Directory.EnumerateFileSystemEntries(_packageFolder, "*", SearchOption.AllDirectories);

    private static bool IsNamed(string path, string name) => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase);

    private static bool IsPackageFile(string path) =>
        string.Equals(Path.GetFileName(path), "package.json", StringComparison.Ordinal)
        || _allowedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
