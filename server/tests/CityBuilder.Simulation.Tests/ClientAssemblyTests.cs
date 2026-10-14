using System.Text.Json.Nodes;

namespace CityBuilder.Simulation.Tests;

/// <summary>
/// Guards the Unity client's assembly definitions and manifest against the dependency table of core-instructions/03-architecture.md and
/// 09-unity-client.md. Only Unity reads these files, so the .NET build cannot see a wrong reference.
/// </summary>
public sealed class ClientAssemblyTests
{
    private const string _newtonsoftDll = "Newtonsoft.Json.dll";
    private const string _simulationPackagePath = "file:../../../shared/Simulation";

    private static readonly string _unityProject = Path.Combine(RepositoryPaths.Root, "client", "CityBuilder");
    private static readonly string _projectFolder = Path.Combine(_unityProject, "Assets", "_Project");

    // Folder under Assets/_Project -> assembly name. The name is also the asmdef file name.
    private static readonly (string Folder, string Name)[] _allAssemblies =
    [
        ("Core", "CityBuilder.Client.Core"),
        ("Net", "CityBuilder.Client.Net"),
        ("Presentation", "CityBuilder.Client.Presentation"),
        ("Tests/EditMode", "CityBuilder.Client.Tests.EditMode"),
        ("Tests/PlayMode", "CityBuilder.Client.Tests.PlayMode"),
    ];

    // The assembly references each runtime assembly may have: exactly the table in 09 (UI Toolkit is an engine module, not a reference).
    public static TheoryData<string, string[]> RuntimeReferences => new()
    {
        { "Core", ["CityBuilder.Simulation"] },
        { "Net", ["CityBuilder.Client.Core"] },
        { "Presentation", ["CityBuilder.Client.Core", "CityBuilder.Client.Net", "Unity.InputSystem"] },
    };

    public static TheoryData<string> RuntimeFolders => new() { "Core", "Net", "Presentation" };

    public static TheoryData<string> TestFolders => new() { "Tests/EditMode", "Tests/PlayMode" };

    // ----- the set of assemblies -----

    [Fact]
    public void ProjectFolder_AssemblyDefinitions_AreTheFiveClientAssemblies()
    {
        // A sixth asmdef would add a layer the dependency table does not know; a missing one would merge code into Assembly-CSharp.
        string[] actual = Directory.GetFiles(_projectFolder, "*.asmdef", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_projectFolder, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected = _allAssemblies
            .Select(assembly => $"{assembly.Folder}/{assembly.Name}.asmdef")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.Empty(Directory.GetFiles(_projectFolder, "*.asmref", SearchOption.AllDirectories));
    }

    [Fact]
    public void AssemblyDefinitions_NameField_MatchesTheFileName()
    {
        foreach ((string folder, string name) in _allAssemblies)
        {
            Assert.Equal(name, (string?)ReadAsmdef(folder, name)["name"]);
        }
    }

    // ----- runtime assemblies: the dependency table -----

    [Theory]
    [MemberData(nameof(RuntimeReferences))]
    public void RuntimeAssembly_References_AreExactlyTheDependencyTable(string folder, string[] expected)
    {
        // Core may use only Simulation, Net only Core, Presentation only Core, Net and the Input System. A server project or a test
        // assembly in this list would break the direction of 03.
        JsonObject asmdef = ReadAsmdef(folder);

        Assert.Equal(expected.Order(StringComparer.Ordinal), Strings(asmdef, "references").Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(RuntimeFolders))]
    public void RuntimeAssembly_Restrictions_AreAbsent(string folder)
    {
        // The client assemblies compile for every platform and are named explicitly by whoever uses them (nothing auto-references them,
        // so Assembly-CSharp, which holds the template's sample scripts, cannot depend on them).
        JsonObject asmdef = ReadAsmdef(folder);

        Assert.False(BoolOf(asmdef, "autoReferenced") ?? true);
        Assert.Empty(Strings(asmdef, "includePlatforms"));
        Assert.Empty(Strings(asmdef, "excludePlatforms"));
        Assert.Empty(Strings(asmdef, "defineConstraints"));
        Assert.Empty(Strings(asmdef, "versionDefines"));
    }

    [Theory]
    [MemberData(nameof(RuntimeFolders))]
    public void RuntimeAssembly_PrecompiledLibraries_AreListedExplicitly(string folder)
    {
        // With overrideReferences off, an assembly sees every auto-referenced precompiled DLL of every package (Newtonsoft, Mono.Cecil, the
        // DLLs of the pre-release AI packages ...), so the table in 09 would not hold for them. With it on, only the listed DLLs are
        // visible: Newtonsoft for Net, nothing for Core and Presentation.
        JsonObject asmdef = ReadAsmdef(folder);
        string[] expected = folder == "Net" ? [_newtonsoftDll] : [];

        Assert.True(BoolOf(asmdef, "overrideReferences"));
        Assert.Equal(expected, Strings(asmdef, "precompiledReferences"));
    }

    // ----- test assemblies -----

    [Theory]
    [MemberData(nameof(TestFolders))]
    public void TestAssembly_Setup_IsTheUnityTestRunnerSetup(string folder)
    {
        JsonObject asmdef = ReadAsmdef(folder);

        Assert.False(BoolOf(asmdef, "autoReferenced") ?? true);
        Assert.True(BoolOf(asmdef, "overrideReferences"));
        Assert.Equal(["nunit.framework.dll"], Strings(asmdef, "precompiledReferences"));
        Assert.Equal(["UNITY_INCLUDE_TESTS"], Strings(asmdef, "defineConstraints"));
        Assert.Contains("UnityEngine.TestRunner", Strings(asmdef, "references"));
        Assert.Contains("UnityEditor.TestRunner", Strings(asmdef, "references"));
    }

    [Fact]
    public void TestAssemblies_Platforms_EditModeIsEditorOnlyAndPlayModeIsEverywhere()
    {
        Assert.Equal(["Editor"], Strings(ReadAsmdef("Tests/EditMode"), "includePlatforms"));
        Assert.Empty(Strings(ReadAsmdef("Tests/PlayMode"), "includePlatforms"));
    }

    [Theory]
    [MemberData(nameof(TestFolders))]
    public void TestAssembly_References_NameTheSimulationAndTheThreeRuntimeAssemblies(string folder)
    {
        // The tests exist to test these four. No test assembly refers to the other one or to anything on the server.
        string[] references = Strings(ReadAsmdef(folder), "references")
            .Where(reference => !reference.EndsWith("TestRunner", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["CityBuilder.Client.Core", "CityBuilder.Client.Net", "CityBuilder.Client.Presentation", "CityBuilder.Simulation"],
            references);
    }

    // ----- Packages/manifest.json -----

    [Fact]
    public void Manifest_SimulationPackage_PointsAtTheSharedPackageFolder()
    {
        // Unity resolves a file: path relative to the Packages folder; it must land on shared/Simulation, where package.json is.
        string? dependency = (string?)ReadManifestDependencies()["com.citybuilder.simulation"];
        string resolved = Path.GetFullPath(Path.Combine(_unityProject, "Packages", dependency!["file:".Length..]));

        Assert.Equal(_simulationPackagePath, dependency);
        Assert.Equal(Path.Combine(RepositoryPaths.Root, "shared", "Simulation"), resolved);
        Assert.True(File.Exists(Path.Combine(resolved, "package.json")));
    }

    [Fact]
    public void Manifest_Dependencies_IncludeNewtonsoftInputSystemAndWebRequests()
    {
        // The asmdefs use Newtonsoft and the Input System, so both are named directly, not left to arrive as another package's dependency.
        // ApiClient (M0-37) is built on UnityWebRequest, a built-in module: a module is enabled only when the manifest or a dependency asks
        // for it, and the AI packages that had enabled it by accident are gone (decision 025). A compile error in M0-04's check showed this.
        JsonObject dependencies = ReadManifestDependencies();

        Assert.NotNull(dependencies["com.unity.nuget.newtonsoft-json"]);
        Assert.NotNull(dependencies["com.unity.inputsystem"]);
        Assert.NotNull(dependencies["com.unity.modules.unitywebrequest"]);
    }

    [Theory]
    [InlineData("com.unity.ai.assistant")]
    [InlineData("com.unity.ai.inference")]
    [InlineData("com.unity.pipeline")]
    public void Manifest_RemovedPreReleasePackages_AreAbsent(string package)
    {
        // The owner removed these in M0-04 (G-Dependency, decision 025). The Unity Hub or Editor can add them again by itself; a package
        // that is not on the approved list in core-instructions/14 needs the owner's yes first.
        Assert.Null(ReadManifestDependencies()[package]);
    }

    // ----- helpers -----

    private static JsonObject ReadAsmdef(string folder)
    {
        string name = _allAssemblies.Single(assembly => assembly.Folder == folder).Name;
        return ReadAsmdef(folder, name);
    }

    private static JsonObject ReadAsmdef(string folder, string name) =>
        ReadJsonObject(Path.Combine(_projectFolder, folder, name + ".asmdef"));

    private static JsonObject ReadManifestDependencies() =>
        ReadJsonObject(Path.Combine(_unityProject, "Packages", "manifest.json"))["dependencies"]!.AsObject();

    private static JsonObject ReadJsonObject(string path)
    {
        JsonNode? root = JsonNode.Parse(File.ReadAllText(path));

        Assert.NotNull(root);
        return root.AsObject();
    }

    private static bool? BoolOf(JsonObject json, string propertyName) => (bool?)json[propertyName];

    // Unity treats a missing array as an empty one.
    private static string[] Strings(JsonObject json, string propertyName) =>
        (json[propertyName]?.AsArray() ?? []).Select(node => (string)node!).ToArray();
}
