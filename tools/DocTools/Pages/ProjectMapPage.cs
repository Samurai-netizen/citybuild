using System.Text.Json;
using System.Xml.Linq;

namespace CityBuilder.DocTools.Pages;

/// <summary>
/// <c>project-map.md</c>: the .NET projects and the Unity assemblies with the references between them, as Mermaid diagrams and tables, read
/// from the <c>.csproj</c> and <c>.asmdef</c> files. The intended dependency rules are in <c>core-instructions/03-architecture.md</c>.
/// </summary>
public static class ProjectMapPage
{
    /// <summary>The page's file name.</summary>
    public const string FileName = "project-map.md";

    /// <summary>Where the .NET projects are, relative to the repository root.</summary>
    private static readonly string[] _projectFolders = ["server", "shared", "tools"];

    /// <summary>Where the project's own Unity assemblies are; Unity's packages and the template's samples are not the project's.</summary>
    private static readonly string[] _assemblyFolders = ["client/CityBuilder/Assets/_Project", "shared/Simulation"];

    /// <summary>Generates the page.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The page.</returns>
    public static GeneratedPage Generate(string root)
    {
        IReadOnlyList<DotNetProject> projects = ReadProjects(root);
        IReadOnlyList<UnityAssembly> assemblies = ReadAssemblies(root);
        var page = new MarkdownPage("Project map", "the `.csproj` files under `server/`, `shared/` and `tools/`, and the `.asmdef` files of the Unity project and `shared/Simulation/`");

        page.Heading(2, ".NET projects")
            .Paragraph("An arrow points from a project to a project it references. The server's solution is `server/CityBuilder.slnx`.")
            .Mermaid(Diagram(projects.Select(p => (p.Name, p.Group, (IReadOnlyList<string>)p.ProjectReferences))))
            .Table(
                ["Project", "Path", "Kind", "Project references", "Packages"],
                projects.Select(p => (IReadOnlyList<string>)
                [
                    MarkdownPage.Code(p.Name), MarkdownPage.Code(p.Path), p.Kind, CodeList(p.ProjectReferences), CodeList(p.Packages),
                ]));

        var ownAssemblies = new HashSet<string>(assemblies.Select(a => a.Name), StringComparer.Ordinal);
        page.Heading(2, "Unity assemblies")
            .Paragraph("An arrow points from an assembly to one of the project's assemblies it references; the other references (Unity's packages) are in the table. " +
                       "`CityBuilder.Simulation` is the same source as the .NET project of that name.")
            .Mermaid(Diagram(assemblies.Select(a => (a.Name, a.Group, (IReadOnlyList<string>)[.. a.References.Where(ownAssemblies.Contains)]))))
            .Table(
                ["Assembly", "Path", "Platforms", "Project references", "Other references"],
                assemblies.Select(a => (IReadOnlyList<string>)
                [
                    MarkdownPage.Code(a.Name), MarkdownPage.Code(a.Path), a.Platforms,
                    CodeList([.. a.References.Where(ownAssemblies.Contains)]), CodeList([.. a.References.Where(r => !ownAssemblies.Contains(r))]),
                ]));

        return page.ToPage(FileName);
    }

    /// <summary>A Mermaid flowchart: one subgraph per group, one node per project or assembly, one arrow per reference.</summary>
    /// <param name="nodes">Each node's name, group and the names it references, in the page's order.</param>
    /// <returns>The diagram's lines.</returns>
    private static List<string> Diagram(IEnumerable<(string Name, string Group, IReadOnlyList<string> References)> nodes)
    {
        List<(string Name, string Group, IReadOnlyList<string> References)> all = [.. nodes];
        var lines = new List<string> { "flowchart LR" };
        foreach (IGrouping<string, (string Name, string Group, IReadOnlyList<string> References)> group in
                 all.GroupBy(node => node.Group).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            lines.Add($"  subgraph {NodeId(group.Key)}[\"{group.Key}\"]");
            lines.AddRange(group.Select(node => $"    {NodeId(node.Name)}[\"{node.Name}\"]"));
            lines.Add("  end");
        }

        foreach ((string name, _, IReadOnlyList<string> references) in all)
        {
            lines.AddRange(references.Select(reference => $"  {NodeId(name)} --> {NodeId(reference)}"));
        }

        return lines;
    }

    /// <summary>A Mermaid node id: the name with every character that isn't a letter or digit replaced by an underscore.</summary>
    /// <param name="name">A project, assembly or group name.</param>
    /// <returns>The id.</returns>
    private static string NodeId(string name) => "n_" + new string([.. name.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')]);

    /// <summary>Names as a comma-separated list of inline code, or a dash for none.</summary>
    /// <param name="names">The names.</param>
    /// <returns>The cell.</returns>
    private static string CodeList(IReadOnlyList<string> names) => names.Count == 0 ? "—" : string.Join(", ", names.Select(MarkdownPage.Code));

    /// <summary>Reads every <c>.csproj</c> in <see cref="_projectFolders"/>.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The projects, ordinal by path.</returns>
    private static List<DotNetProject> ReadProjects(string root)
    {
        var projects = new List<DotNetProject>();
        foreach (string path in RepositoryFiles.Find(root, _projectFolders, "*.csproj"))
        {
            XDocument project = XDocument.Load(Path.Combine(root, path));
            List<string> references = [.. Items(project, "ProjectReference")
                .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
                .Order(StringComparer.Ordinal)];
            List<string> packages = [.. Items(project, "PackageReference").Order(StringComparer.Ordinal)];
            string name = Path.GetFileNameWithoutExtension(path);
            projects.Add(new DotNetProject(name, path, GroupOf(path), KindOf(project, name), references, packages));
        }

        return projects;
    }

    /// <summary>The <c>Include</c> values of every item of one type.</summary>
    /// <param name="project">The project file.</param>
    /// <param name="itemType">The item type, for example <c>ProjectReference</c>.</param>
    /// <returns>The values, in file order.</returns>
    private static IEnumerable<string> Items(XDocument project, string itemType) =>
        project.Descendants(itemType).Select(item => (string?)item.Attribute("Include")).OfType<string>();

    /// <summary>What a project builds: a web app, a test project, a console tool or a library.</summary>
    /// <param name="project">The project file.</param>
    /// <param name="name">The project's name.</param>
    /// <returns>The kind, in words.</returns>
    private static string KindOf(XDocument project, string name)
    {
        string sdk = (string?)project.Root?.Attribute("Sdk") ?? string.Empty;
        bool executable = project.Descendants("OutputType").Any(output => output.Value == "Exe");
        string? framework = project.Descendants("TargetFramework").Select(f => f.Value).FirstOrDefault();
        string kind = sdk == "Microsoft.NET.Sdk.Web" ? "web app"
            : name.EndsWith(".Tests", StringComparison.Ordinal) ? "tests"
            : executable ? "console"
            : "library";
        return framework is null ? kind : $"{kind} ({framework})";
    }

    /// <summary>The group a path belongs to in the diagram: its first two folders, or the first one for <c>shared/</c> and <c>tools/</c>.</summary>
    /// <param name="path">The file's path relative to the root.</param>
    /// <returns>The group, for example <c>server/src</c>.</returns>
    private static string GroupOf(string path)
    {
        string[] parts = path.Split('/');
        return parts[0] == "server" && parts.Length > 2 ? parts[0] + "/" + parts[1] : parts[0];
    }

    /// <summary>Reads every <c>.asmdef</c> in <see cref="_assemblyFolders"/>.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The assemblies, ordinal by path.</returns>
    private static List<UnityAssembly> ReadAssemblies(string root)
    {
        var assemblies = new List<UnityAssembly>();
        foreach (string path in RepositoryFiles.Find(root, _assemblyFolders, "*.asmdef"))
        {
            using JsonDocument definition = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, path)));
            JsonElement asmdef = definition.RootElement;
            string name = asmdef.GetProperty("name").GetString() ?? Path.GetFileNameWithoutExtension(path);
            List<string> references = [.. StringsOf(asmdef, "references").Order(StringComparer.Ordinal)];
            List<string> platforms = [.. StringsOf(asmdef, "includePlatforms")];
            string group = path.StartsWith("shared/", StringComparison.Ordinal) ? "shared" : "client";
            assemblies.Add(new UnityAssembly(name, path, group, platforms.Count == 0 ? "all" : string.Join(", ", platforms), references));
        }

        return assemblies;
    }

    /// <summary>The strings of a JSON array property, or none when it is missing.</summary>
    /// <param name="element">The JSON object.</param>
    /// <param name="property">The property's name.</param>
    /// <returns>The strings, in file order.</returns>
    private static IEnumerable<string> StringsOf(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement array)
            ? array.EnumerateArray().Select(item => item.GetString()).OfType<string>()
            : [];

    /// <summary>One .NET project as the page shows it.</summary>
    /// <param name="Name">The project's name (its file name).</param>
    /// <param name="Path">The file's path relative to the root.</param>
    /// <param name="Group">Its group in the diagram.</param>
    /// <param name="Kind">What it builds, in words.</param>
    /// <param name="ProjectReferences">The projects it references, by name, ordinal.</param>
    /// <param name="Packages">The packages it references, ordinal.</param>
    private sealed record DotNetProject(
        string Name, string Path, string Group, string Kind, IReadOnlyList<string> ProjectReferences, IReadOnlyList<string> Packages);

    /// <summary>One Unity assembly as the page shows it.</summary>
    /// <param name="Name">The assembly's name.</param>
    /// <param name="Path">The <c>.asmdef</c> path relative to the root.</param>
    /// <param name="Group">Its group in the diagram.</param>
    /// <param name="Platforms">The platforms it is built for, or "all".</param>
    /// <param name="References">The assemblies it references by name, ordinal.</param>
    private sealed record UnityAssembly(string Name, string Path, string Group, string Platforms, IReadOnlyList<string> References);
}
