using System.Text.Json.Nodes;

namespace CityBuilder.Simulation.Tests;

/// <summary>
/// Guards what only Unity sees of the shared package (the asmdef and package.json) and the layout rules of
/// core-instructions/03-architecture.md. The .NET build cannot check these.
/// </summary>
public sealed class SimulationPackageTests
{
    /// <summary>What a Unity package of sources may hold; anything else (a DLL, a PDB, a restore cache, a project file) is build output or clutter.</summary>
    private static readonly string[] _allowedExtensions = [".cs", ".asmdef", ".meta", ".md"];

    /// <summary>The engine's Unity package folder, <c>shared/Simulation</c>.</summary>
    private static readonly string _packageFolder = Path.Combine(RepositoryPaths.Root, "shared", "Simulation");
    /// <summary>The package's <c>Runtime</c> folder, which holds the asmdef and every source file.</summary>
    private static readonly string _runtimeFolder = Path.Combine(_packageFolder, "Runtime");

    // ----- Runtime/CityBuilder.Simulation.asmdef -----

    /// <summary>The asmdef keeps <c>noEngineReferences</c> on, so no engine code can use Unity's API.</summary>
    [Fact]
    public void Asmdef_NoEngineReferences_IsTrue()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.True(BoolOf(asmdef, "noEngineReferences") == true);
    }

    /// <summary>The asmdef references no assembly and no precompiled library: the engine depends on nothing.</summary>
    [Fact]
    public void Asmdef_AssemblyAndLibraryReferences_AreAbsent()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.Empty(ArrayOf(asmdef, "references"));
        Assert.Empty(ArrayOf(asmdef, "precompiledReferences"));
        Assert.False(BoolOf(asmdef, "overrideReferences") ?? false);
    }

    /// <summary>The asmdef has no platform filter and no define constraint, so the engine compiles into every Unity build target.</summary>
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

    /// <summary>The Unity assembly has the .NET assembly's name and the root namespace <c>CityBuilder.Simulation</c>.</summary>
    [Fact]
    public void Asmdef_Name_MatchesTheDotNetAssemblyAndRootNamespace()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.Equal(typeof(ItemId).Assembly.GetName().Name, (string?)asmdef["name"]);
        Assert.Equal("CityBuilder.Simulation", (string?)asmdef["rootNamespace"]);
    }

    /// <summary>The engine is auto-referenced, so the client assemblies and their tests can use it.</summary>
    [Fact]
    public void Asmdef_AutoReferenced_IsTrue()
    {
        JsonObject asmdef = ReadAsmdef();

        Assert.True(BoolOf(asmdef, "autoReferenced") == true);
    }

    /// <summary>The package has exactly one asmdef, in <c>Runtime/</c>, and no asmref, so Unity compiles one assembly as the .NET project does.</summary>
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

    /// <summary>The package is named <c>com.citybuilder.simulation</c>, the name the client's manifest uses.</summary>
    [Fact]
    public void PackageJson_Name_IsTheCityBuilderSimulationPackage()
    {
        Assert.Equal("com.citybuilder.simulation", (string?)ReadPackageJson()["name"]);
    }

    /// <summary>The package requires Unity 6.3 (<c>6000.3</c>), the client's editor line.</summary>
    [Fact]
    public void PackageJson_UnityField_RequiresUnity63()
    {
        Assert.Equal("6000.3", (string?)ReadPackageJson()["unity"]);
    }

    /// <summary>The package version is <c>major.minor.patch</c> in ASCII digits.</summary>
    [Fact]
    public void PackageJson_Version_IsSemantic()
    {
        // ASCII digits and \z on purpose: \d matches any Unicode digit and $ also matches before a final newline.
        Assert.Matches(@"^[0-9]+\.[0-9]+\.[0-9]+\z", (string?)ReadPackageJson()["version"] ?? string.Empty);
    }

    // ----- layout -----

    /// <summary>No <c>bin</c> or <c>obj</c> folder lies in the package: Unity would import it.</summary>
    [Fact]
    public void PackageFolder_BuildOutputFolders_AreAbsent()
    {
        // Unity imports every file of a package folder, so build output must stay in shared/Simulation.Build.
        string[] buildOutput = Directory.EnumerateDirectories(_packageFolder, "*", SearchOption.AllDirectories)
            .Where(path => IsNamed(path, "bin") || IsNamed(path, "obj"))
            .ToArray();

        Assert.Empty(buildOutput);
    }

    /// <summary>The package holds only sources, asmdefs, .meta files, Markdown and <c>package.json</c>.</summary>
    [Fact]
    public void PackageFolder_Files_AreSourcesAndPackageMetadataOnly()
    {
        string[] unexpected = Directory.EnumerateFiles(_packageFolder, "*", SearchOption.AllDirectories)
            .Where(path => !IsPackageFile(path))
            .Select(path => Path.GetRelativePath(_packageFolder, path))
            .ToArray();

        Assert.Empty(unexpected);
    }

    /// <summary>Every file and folder has its committed .meta file, so the asset keeps one identity on every clone.</summary>
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

    /// <summary>No .meta file is left over from a removed file or folder.</summary>
    [Fact]
    public void PackageFolder_EveryMetaFile_BelongsToAFileOrFolder()
    {
        string[] orphans = Directory.EnumerateFiles(_packageFolder, "*.meta", SearchOption.AllDirectories)
            .Where(meta => !File.Exists(meta[..^".meta".Length]) && !Directory.Exists(meta[..^".meta".Length]))
            .Select(path => Path.GetRelativePath(_packageFolder, path))
            .ToArray();

        Assert.Empty(orphans);
    }

    /// <summary>Every .meta file has a well-formed GUID of its own; a copied one would give two assets the same identity.</summary>
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

    /// <summary>The runtime folder holds source files, so the checks over them can't pass on an empty list.</summary>
    [Fact]
    public void RuntimeSources_Folder_ContainsSourceFiles()
    {
        // Keeps the check below from passing on an empty list (a wrong path would find nothing).
        Assert.NotEmpty(RuntimeSources());
    }

    /// <summary>Every source starts with <c>#nullable enable</c>, the only way Unity turns nullable annotations on.</summary>
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

    /// <summary>Reads the engine's asmdef.</summary>
    private static JsonObject ReadAsmdef() => ReadJsonObject(Path.Combine(_runtimeFolder, "CityBuilder.Simulation.asmdef"));

    /// <summary>Reads the engine's <c>package.json</c>.</summary>
    private static JsonObject ReadPackageJson() => ReadJsonObject(Path.Combine(_packageFolder, "package.json"));

    /// <summary>Parses a JSON file whose root is an object; fails the test when it isn't.</summary>
    private static JsonObject ReadJsonObject(string path)
    {
        JsonNode? root = JsonNode.Parse(File.ReadAllText(path));

        Assert.NotNull(root);
        return root.AsObject();
    }

    /// <summary>A boolean property of a JSON object, or null when it is absent.</summary>
    private static bool? BoolOf(JsonObject json, string propertyName) => (bool?)json[propertyName];

    /// <summary>An array property of a JSON object; a missing array is empty, as Unity treats it.</summary>
    private static JsonArray ArrayOf(JsonObject json, string propertyName) => json[propertyName]?.AsArray() ?? [];

    /// <summary>Every C# file under the runtime folder.</summary>
    private static string[] RuntimeSources() => Directory.GetFiles(_runtimeFolder, "*.cs", SearchOption.AllDirectories);

    /// <summary>Every file and folder below the package root. The root itself has no .meta file: it is the package, not an asset in it.</summary>
    private static IEnumerable<string> PackageEntries() =>
        Directory.EnumerateFileSystemEntries(_packageFolder, "*", SearchOption.AllDirectories);

    /// <summary>Whether a path's last part has a name, ignoring case (a <c>Bin</c> folder is build output too).</summary>
    private static bool IsNamed(string path, string name) => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a file may be in the package: <c>package.json</c> or one of <see cref="_allowedExtensions"/>.</summary>
    private static bool IsPackageFile(string path) =>
        string.Equals(Path.GetFileName(path), "package.json", StringComparison.Ordinal)
        || _allowedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
