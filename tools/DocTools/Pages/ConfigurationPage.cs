using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CityBuilder.Api.Auth;

namespace CityBuilder.DocTools.Pages;

/// <summary>
/// <c>configuration.md</c>: every configuration key of the server. The keys come from the API's options classes (every public class with a
/// <c>SectionName</c> constant), each property's default from a new instance, the committed values from <c>appsettings.json</c> and
/// <c>appsettings.Development.json</c>, and the meaning and validation from the property's doc comment. The validation itself runs at
/// startup (<c>Program.cs</c>, <c>ValidateOnStart</c>); the doc comments state its ranges.
/// </summary>
public static class ConfigurationPage
{
    /// <summary>The page's file name.</summary>
    public const string FileName = "configuration.md";

    /// <summary>The API project's folder, relative to the repository root.</summary>
    private const string _apiFolder = "server/src/CityBuilder.Api";

    /// <summary>Generates the page.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The page.</returns>
    public static GeneratedPage Generate(string root)
    {
        Assembly api = typeof(AuthOptions).Assembly;
        var docs = new XmlDocs(api);
        Dictionary<string, string> committed = ReadSettings(Path.Combine(root, _apiFolder, "appsettings.json"));
        Dictionary<string, string> development = ReadSettings(Path.Combine(root, _apiFolder, "appsettings.Development.json"));
        var keys = new List<Setting>();
        foreach (Type options in api.GetExportedTypes().Where(type => type.IsClass && SectionNameOf(type) is not null)
                     .OrderBy(type => SectionNameOf(type), StringComparer.Ordinal))
        {
            Collect(options, Activator.CreateInstance(options)!, SectionNameOf(options)!, string.Empty, docs, keys);
        }

        var page = new MarkdownPage("Configuration reference",
            "the options classes of `server/src/CityBuilder.Api/` and its `appsettings.json` and `appsettings.Development.json`");
        page.Paragraph("Any key can be set with an environment variable (`:` becomes `__`, for example `Auth__SigningKey`) or a command-line " +
                       "argument (`-- --Key=value` after `dotnet run`). A blank cell means the file doesn't set the key. Secrets (the connection " +
                       "string, the signing key) come from user-secrets or the environment, never a committed file (`docs/getting-started.md` → Database).")
            .Table(["Key", "Type", "Default", "appsettings.json", "Development", "Meaning and validation"], keys.Select(setting => (IReadOnlyList<string>)
            [
                MarkdownPage.Code(setting.Key), MarkdownPage.Code(setting.Type), setting.Default,
                Committed(committed, setting.Key), Committed(development, setting.Key), setting.Meaning,
            ]));

        var known = new HashSet<string>(keys.Select(setting => setting.Key), StringComparer.OrdinalIgnoreCase);
        page.Heading(2, "Other keys in the settings files")
            .Paragraph("ASP.NET Core's own keys, which no options class of the API binds.")
            .Table(["Key", "appsettings.json", "Development"], committed.Keys.Union(development.Keys).Where(key => !known.Contains(key))
                .Order(StringComparer.Ordinal)
                .Select(key => (IReadOnlyList<string>)[MarkdownPage.Code(key), Committed(committed, key), Committed(development, key)]));
        return page.ToPage(FileName);
    }

    /// <summary>
    /// Adds a row for each settable property of an options object, and walks into a property that is itself a settings object (such as
    /// <c>RateLimits:Commands:Burst</c>).
    /// </summary>
    /// <param name="type">The object's type.</param>
    /// <param name="instance">A new instance, whose values are the defaults.</param>
    /// <param name="prefix">The key so far, for example <c>RateLimits:Commands</c>.</param>
    /// <param name="context">
    /// The summaries of the properties that lead to the object, key stripped, for example "Every command endpoint, counted together per player."
    /// for <c>RateLimits:Commands</c>: the leaf's own summary (such as a <c>RateRule</c>'s) is generic, the path says what it limits.
    /// </param>
    /// <param name="docs">The API's doc comments.</param>
    /// <param name="keys">The rows so far.</param>
    private static void Collect(Type type, object instance, string prefix, string context, XmlDocs docs, List<Setting> keys)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(property => property.CanWrite).OrderBy(property => property.MetadataToken))
        {
            object? value = property.GetValue(instance);
            string key = KeyOf(type, property, prefix);
            string meaning = Join(context, StripKey(docs.Summary(property), key));
            if (IsSettingsObject(property.PropertyType) && value is not null)
            {
                Collect(property.PropertyType, value, key, meaning, docs, keys);
                continue;
            }

            keys.Add(new Setting(key, TypeName(property.PropertyType), DefaultOf(value), meaning));
        }
    }

    /// <summary>
    /// A property's configuration key: the section and the property's path, except the connection string, which ASP.NET Core reads from
    /// <c>ConnectionStrings:</c> and the options class's <c>ConnectionStringName</c> constant.
    /// </summary>
    /// <param name="type">The options class.</param>
    /// <param name="property">The property.</param>
    /// <param name="prefix">The section path.</param>
    /// <returns>The key, for example <c>Database:LockTimeoutMilliseconds</c>.</returns>
    private static string KeyOf(Type type, PropertyInfo property, string prefix) =>
        property.Name == "ConnectionString" && type.GetField("ConnectionStringName")?.GetRawConstantValue() is string name
            ? "ConnectionStrings:" + name
            : prefix + ":" + property.Name;

    /// <summary>Whether a property's type is a nested settings object rather than a value: a class of the API other than <see cref="string"/>.</summary>
    /// <param name="type">The property's type.</param>
    /// <returns>True for a nested object.</returns>
    private static bool IsSettingsObject(Type type) => type.IsClass && type != typeof(string) && type.Assembly == typeof(AuthOptions).Assembly;

    /// <summary>The <c>SectionName</c> constant of an options class, or null when it has none.</summary>
    /// <param name="type">The class.</param>
    /// <returns>The section's name.</returns>
    private static string? SectionNameOf(Type type) =>
        type.GetField("SectionName", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } field ? field.GetRawConstantValue() as string : null;

    /// <summary>A default value as code; "(empty)" for an empty string, which is how a required secret starts.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The cell.</returns>
    private static string DefaultOf(object? value) => value switch
    {
        null => "—",
        string { Length: 0 } => "(empty)",
        bool flag => MarkdownPage.Code(flag ? "true" : "false"),
        _ => MarkdownPage.Code(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    /// <summary>The C# keyword of a property's type, or its name.</summary>
    /// <param name="type">The type.</param>
    /// <returns>For example <c>int</c>, <c>bool</c>, <c>string</c>.</returns>
    private static string TypeName(Type type) => Type.GetTypeCode(type) switch
    {
        TypeCode.Boolean => "bool",
        TypeCode.Int32 => "int",
        TypeCode.Int64 => "long",
        TypeCode.String => "string",
        _ => type.Name,
    };

    /// <summary>A summary without the key it starts with (most say "<c>Key</c>: …"), since the key has its own column.</summary>
    /// <param name="meaning">The summary as Markdown.</param>
    /// <param name="key">The key.</param>
    /// <returns>The rest of the summary, capitalised.</returns>
    private static string StripKey(string meaning, string key)
    {
        string code = MarkdownPage.Code(key);
        if (!meaning.StartsWith(code, StringComparison.Ordinal))
        {
            return meaning;
        }

        string rest = meaning[code.Length..].TrimStart(':', ',', ' ');
        return rest.Length == 0 ? rest : char.ToUpperInvariant(rest[0]) + rest[1..];
    }

    /// <summary>Two sentences joined by a space; either may be empty.</summary>
    /// <param name="first">The first.</param>
    /// <param name="second">The second.</param>
    /// <returns>The text.</returns>
    private static string Join(string first, string second) => first.Length == 0 ? second : second.Length == 0 ? first : first + " " + second;

    /// <summary>A key's value in a settings file as code, or an empty cell when the file doesn't set it.</summary>
    /// <param name="settings">The file's keys and values.</param>
    /// <param name="key">The key.</param>
    /// <returns>The cell.</returns>
    private static string Committed(Dictionary<string, string> settings, string key) =>
        settings.TryGetValue(key, out string? value) ? MarkdownPage.Code(value) : string.Empty;

    /// <summary>A settings file's values flattened to keys like <c>Auth:DevLoginEnabled</c>, as the configuration system reads them.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The keys (case-insensitive, as configuration keys are) and their values as text.</returns>
    private static Dictionary<string, string> ReadSettings(string path)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(path));
        Flatten(file.RootElement, string.Empty, settings);
        return settings;
    }

    /// <summary>Adds an element's leaf values under their keys.</summary>
    /// <param name="element">The element.</param>
    /// <param name="prefix">Its key, or empty for the root.</param>
    /// <param name="settings">The keys so far.</param>
    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> settings)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Flatten(property.Value, prefix.Length == 0 ? property.Name : prefix + ":" + property.Name, settings);
            }
        }
        else
        {
            settings[prefix] = element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();
        }
    }

    /// <summary>One configuration key as the page shows it.</summary>
    /// <param name="Key">The key.</param>
    /// <param name="Type">Its value's type.</param>
    /// <param name="Default">The options class's default, as a cell.</param>
    /// <param name="Meaning">The doc comment, as Markdown.</param>
    private sealed record Setting(string Key, string Type, string Default, string Meaning);
}
