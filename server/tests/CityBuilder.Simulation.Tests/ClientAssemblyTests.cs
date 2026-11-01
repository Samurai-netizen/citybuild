using System.Text.Json.Nodes;

namespace CityBuilder.Simulation.Tests;

/// <summary>
/// Guards the Unity client's assembly definitions and manifest against the dependency table of core-instructions/03-architecture.md and
/// 09-unity-client.md. Only Unity reads these files, so the .NET build cannot see a wrong reference.
/// </summary>
public sealed class ClientAssemblyTests
{
    /// <summary>The only precompiled DLL a client assembly may list: Newtonsoft, and only <c>CityBuilder.Client.Net</c> uses it (<c>09</c>).</summary>
    private const string _newtonsoftDll = "Newtonsoft.Json.dll";
    /// <summary>The manifest's path to the shared engine package, relative to the Unity project's <c>Packages</c> folder.</summary>
    private const string _simulationPackagePath = "file:../../../shared/Simulation";

    /// <summary>The Unity project's folder, <c>client/CityBuilder</c>.</summary>
    private static readonly string _unityProject = Path.Combine(RepositoryPaths.Root, "client", "CityBuilder");
    /// <summary>The folder of the project's own code, <c>Assets/_Project</c>, where the six assembly definitions live.</summary>
    private static readonly string _projectFolder = Path.Combine(_unityProject, "Assets", "_Project");

    /// <summary>The six client assemblies: the folder under <c>Assets/_Project</c> and the assembly name, which is also the asmdef file's name.</summary>
    private static readonly (string Folder, string Name)[] _allAssemblies =
    [
        ("Core", "CityBuilder.Client.Core"),
        ("Net", "CityBuilder.Client.Net"),
        ("Presentation", "CityBuilder.Client.Presentation"),
        ("Editor", "CityBuilder.Client.Editor"),
        ("Tests/EditMode", "CityBuilder.Client.Tests.EditMode"),
        ("Tests/PlayMode", "CityBuilder.Client.Tests.PlayMode"),
    ];

    /// <summary>The assembly references each runtime assembly may have: exactly the table in <c>09</c> (UI Toolkit is an engine module, not a reference).</summary>
    public static TheoryData<string, string[]> RuntimeReferences => new()
    {
        { "Core", ["CityBuilder.Simulation"] },
        { "Net", ["CityBuilder.Client.Core", "CityBuilder.Simulation"] },
        { "Presentation", ["CityBuilder.Client.Core", "CityBuilder.Client.Net", "CityBuilder.Simulation", "Unity.InputSystem"] },
    };

    /// <summary>The folders of the three runtime assemblies, for the theories that check each.</summary>
    public static TheoryData<string> RuntimeFolders => new() { "Core", "Net", "Presentation" };

    /// <summary>The folders of the two test assemblies, for the theories that check each.</summary>
    public static TheoryData<string> TestFolders => new() { "Tests/EditMode", "Tests/PlayMode" };

    // ----- the set of assemblies -----

    /// <summary>The project folder holds exactly the six assembly definitions and no assembly reference file.</summary>
    [Fact]
    public void ProjectFolder_AssemblyDefinitions_AreTheSixClientAssemblies()
    {
        // A seventh asmdef would add a layer the dependency table does not know; a missing one would merge code into Assembly-CSharp.
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

    /// <summary>Each asmdef's <c>name</c> equals its file name, which is how the other asmdefs reference it.</summary>
    [Fact]
    public void AssemblyDefinitions_NameField_MatchesTheFileName()
    {
        foreach ((string folder, string name) in _allAssemblies)
        {
            Assert.Equal(name, (string?)ReadAsmdef(folder, name)["name"]);
        }
    }

    // ----- runtime assemblies: the dependency table -----

    /// <summary>Each runtime assembly references exactly what the dependency table allows it, nothing more and nothing less.</summary>
    [Theory]
    [MemberData(nameof(RuntimeReferences))]
    public void RuntimeAssembly_References_AreExactlyTheDependencyTable(string folder, string[] expected)
    {
        // Core may use only Simulation, Net only Core and Simulation, Presentation only Core, Net, Simulation and the Input System. A server project or a test
        // assembly in this list would break the direction of 03.
        JsonObject asmdef = ReadAsmdef(folder);

        Assert.Equal(expected.Order(StringComparer.Ordinal), Strings(asmdef, "references").Order(StringComparer.Ordinal));
    }

    /// <summary>A runtime assembly compiles for every platform, has no define constraint, and is not auto-referenced.</summary>
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

    /// <summary>A runtime assembly overrides the precompiled references and lists only its own: Newtonsoft for Net, nothing for Core and Presentation.</summary>
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

    // ----- the editor assembly -----

    /// <summary>The editor assembly (CR-0005-T2) exists only in the Editor and depends on nothing of ours, so no game code can reach it or be reached by it.</summary>
    [Fact]
    public void EditorAssembly_Setup_IsEditorOnlyWithoutReferences()
    {
        JsonObject asmdef = ReadAsmdef("Editor");

        Assert.Equal(["Editor"], Strings(asmdef, "includePlatforms"));
        Assert.Empty(Strings(asmdef, "references"));
        Assert.False(BoolOf(asmdef, "autoReferenced") ?? true);
        Assert.True(BoolOf(asmdef, "overrideReferences"));
        Assert.Empty(Strings(asmdef, "precompiledReferences"));
    }

    // ----- test assemblies -----

    /// <summary>A test assembly has the Unity Test Runner setup: NUnit, the test runner references and the <c>UNITY_INCLUDE_TESTS</c> constraint.</summary>
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

    /// <summary>The EditMode tests run in the Editor only; the PlayMode tests on every platform.</summary>
    [Fact]
    public void TestAssemblies_Platforms_EditModeIsEditorOnlyAndPlayModeIsEverywhere()
    {
        Assert.Equal(["Editor"], Strings(ReadAsmdef("Tests/EditMode"), "includePlatforms"));
        Assert.Empty(Strings(ReadAsmdef("Tests/PlayMode"), "includePlatforms"));
    }

    /// <summary>
    /// A test assembly references the engine and the three runtime assemblies besides the test runner, and nothing on the server. The PlayMode
    /// assembly may also reference the Input System and its test framework (M0-43, owner-approved G-Tests change): the city's input reader
    /// is tested with the package's fake mouse and touch screen. The EditMode assembly stays exact.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestFolders))]
    public void TestAssembly_References_NameTheSimulationAndTheThreeRuntimeAssemblies(string folder)
    {
        // The tests exist to test these four. No test assembly refers to the other one or to anything on the server.
        string[] expected = ["CityBuilder.Client.Core", "CityBuilder.Client.Net", "CityBuilder.Client.Presentation", "CityBuilder.Simulation"];
        if (folder == "Tests/PlayMode")
        {
            expected = [.. expected, "Unity.InputSystem", "Unity.InputSystem.TestFramework"];
        }

        string[] references = Strings(ReadAsmdef(folder), "references")
            .Where(reference => !reference.EndsWith("TestRunner", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, references);
    }

    // ----- Packages/manifest.json -----

    /// <summary>The manifest's <c>file:</c> path resolves to <c>shared/Simulation</c>, where the engine's <c>package.json</c> is.</summary>
    [Fact]
    public void Manifest_SimulationPackage_PointsAtTheSharedPackageFolder()
    {
        // Unity resolves a file: path relative to the Packages folder; it must land on shared/Simulation, where package.json is.
        string? dependency = (string?)ReadManifestDependencies()["com.citybuilder.simulation"];

        Assert.Equal(_simulationPackagePath, dependency);

        // Not null: the assertion above has just shown that it is the file: path.
        string resolved = Path.GetFullPath(Path.Combine(_unityProject, "Packages", dependency!["file:".Length..]));
        Assert.Equal(Path.Combine(RepositoryPaths.Root, "shared", "Simulation"), resolved);
        Assert.True(File.Exists(Path.Combine(resolved, "package.json")));
    }

    /// <summary>The manifest names Newtonsoft, the Input System and the UnityWebRequest module directly, because the assemblies use them.</summary>
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

    /// <summary>The three pre-release AI packages the owner removed in M0-04 stay out of the manifest (decision 025).</summary>
    [Theory]
    [InlineData("com.unity.ai.assistant")]
    [InlineData("com.unity.ai.inference")]
    [InlineData("com.unity.pipeline")]
    public void Manifest_RemovedPreReleasePackages_AreAbsent(string package)
    {
        // The owner removed these in M0-04 (G-Dependency, decision 025). They were in the owner's new project without being asked for, so they can come back with a re-created project; a package
        // that is not on the approved list in core-instructions/14 needs the owner's yes first.
        Assert.Null(ReadManifestDependencies()[package]);
    }

    // ----- helpers -----

    /// <summary>Reads the asmdef of the assembly in a folder of <see cref="_allAssemblies"/>.</summary>
    private static JsonObject ReadAsmdef(string folder)
    {
        string name = _allAssemblies.Single(assembly => assembly.Folder == folder).Name;
        return ReadAsmdef(folder, name);
    }

    /// <summary>Reads the asmdef with a given name in a folder under <c>Assets/_Project</c>.</summary>
    private static JsonObject ReadAsmdef(string folder, string name) =>
        ReadJsonObject(Path.Combine(_projectFolder, folder, name + ".asmdef"));

    /// <summary>The <c>dependencies</c> object of <c>Packages/manifest.json</c>; fails the test when it is missing.</summary>
    private static JsonObject ReadManifestDependencies()
    {
        JsonNode? dependencies = ReadJsonObject(Path.Combine(_unityProject, "Packages", "manifest.json"))["dependencies"];

        Assert.NotNull(dependencies);
        return dependencies.AsObject();
    }

    /// <summary>Parses a JSON file whose root is an object; fails the test when it isn't.</summary>
    private static JsonObject ReadJsonObject(string path)
    {
        JsonNode? root = JsonNode.Parse(File.ReadAllText(path));

        Assert.NotNull(root);
        return root.AsObject();
    }

    /// <summary>A boolean property of a JSON object, or null when it is absent.</summary>
    private static bool? BoolOf(JsonObject json, string propertyName) => (bool?)json[propertyName];

    /// <summary>
    /// A string-array property of a JSON object; a missing array is empty, as Unity treats it. The "!" only silences the compiler: a null entry
    /// comes out as null and then fails the comparison it takes part in, and a non-string entry throws.
    /// </summary>
    private static string[] Strings(JsonObject json, string propertyName) =>
        (json[propertyName]?.AsArray() ?? []).Select(node => (string)node!).ToArray();
}
