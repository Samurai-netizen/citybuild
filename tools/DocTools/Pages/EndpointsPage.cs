using System.Text.Json;

namespace CityBuilder.DocTools.Pages;

/// <summary>
/// <c>endpoints.md</c>: the HTTP API as its OpenAPI document describes it, the file every build of the API writes
/// (<c>server/src/CityBuilder.Api/obj/openapi/CityBuilder.Api.json</c>, <c>BuildTimeDocument</c> in the API): each operation with its auth,
/// request body and success response, then every schema with its properties. The normative contract is
/// <c>core-instructions/05-api-contract.md</c>; the error answers are on <c>errors.md</c>.
/// </summary>
public static class EndpointsPage
{
    /// <summary>The page's file name.</summary>
    public const string FileName = "endpoints.md";

    /// <summary>The OpenAPI document the API's build writes, relative to the repository root.</summary>
    public const string DocumentPath = "server/src/CityBuilder.Api/obj/openapi/CityBuilder.Api.json";

    /// <summary>The prefix of a reference to a schema of the document.</summary>
    private const string _schemaReference = "#/components/schemas/";

    /// <summary>Generates the page.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The page.</returns>
    /// <exception cref="FileNotFoundException">The API has not been built, so there is no document.</exception>
    public static GeneratedPage Generate(string root)
    {
        string path = Path.Combine(root, DocumentPath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{DocumentPath} is missing: build the API first (dotnet build server/CityBuilder.slnx); its build writes the OpenAPI document.", path);
        }

        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement document = file.RootElement;
        var page = new MarkdownPage("HTTP endpoints", $"the OpenAPI {MarkdownPage.Code(document.GetProperty("openapi").GetString())} document the API's build writes (`{DocumentPath}`)");
        page.Paragraph("In the order the server maps them. \"Bearer\" means the request sends the access token in the header `Authorization: Bearer` " +
                       "followed by the token; without a valid token the answer is 401 `AUTHENTICATION_REQUIRED`. Every error answer is Problem Details with a code from " +
                       "[errors.md](errors.md). `POST /api/v1/auth/dev-login` is mapped only in Development with `Auth:DevLoginEnabled` on, and " +
                       "`GET /health` (health checks) is not part of the document.")
            .Table(["Method", "Path", "Auth", "Request body", "Success"], Operations(document).Select(operation => (IReadOnlyList<string>)
            [
                operation.Method, MarkdownPage.Code(operation.Path), operation.Auth, operation.Request, operation.Success,
            ]));

        page.Heading(2, "Schemas")
            .Paragraph("The request and response bodies. The document types numbers as `integer` or `string`, because ASP.NET Core's web JSON " +
                       "defaults also read a number written as a string; this page shows the number's format. A property that isn't required may be left out.");
        if (document.TryGetProperty("components", out JsonElement components) && components.TryGetProperty("schemas", out JsonElement schemas))
        {
            foreach (JsonProperty schema in schemas.EnumerateObject().OrderBy(schema => schema.Name, StringComparer.Ordinal))
            {
                WriteSchema(page, schema.Name, schema.Value);
            }
        }

        return page.ToPage(FileName);
    }

    /// <summary>Every operation of the document, in its order: paths as written, methods as written under each path.</summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>The operations as the page shows them.</returns>
    private static IEnumerable<Operation> Operations(JsonElement document)
    {
        foreach (JsonProperty path in document.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty method in path.Value.EnumerateObject())
            {
                JsonElement operation = method.Value;
                string auth = operation.TryGetProperty("security", out JsonElement security) && security.GetArrayLength() > 0
                    ? string.Join(", ", security.EnumerateArray().SelectMany(requirement => requirement.EnumerateObject().Select(scheme => scheme.Name)))
                    : "none";
                string request = operation.TryGetProperty("requestBody", out JsonElement body) ? Content(body) : "—";
                string success = string.Join("; ", operation.GetProperty("responses").EnumerateObject()
                    .Where(response => response.Name.StartsWith('2'))
                    .Select(response => response.Name + (response.Value.TryGetProperty("content", out _) ? " " + Content(response.Value) : string.Empty)));
                yield return new Operation(method.Name.ToUpperInvariant(), path.Name, auth, request, success);
            }
        }
    }

    /// <summary>A body's schema per media type, for example <c>PlaceBuildingRequest</c> (application/json).</summary>
    /// <param name="body">A request body or response with <c>content</c>.</param>
    /// <returns>The cell.</returns>
    private static string Content(JsonElement body) =>
        body.TryGetProperty("content", out JsonElement content)
            ? string.Join(", ", content.EnumerateObject().Select(media => TypeOf(media.Value.GetProperty("schema")).Text + " (" + media.Name + ")"))
            : "—";

    /// <summary>One schema's section: its properties with type, required and nullable.</summary>
    /// <param name="page">The page.</param>
    /// <param name="name">The schema's name.</param>
    /// <param name="schema">The schema.</param>
    private static void WriteSchema(MarkdownPage page, string name, JsonElement schema)
    {
        page.Heading(3, name);
        var required = new HashSet<string>(
            schema.TryGetProperty("required", out JsonElement list) ? list.EnumerateArray().Select(item => item.GetString()!) : [], StringComparer.Ordinal);
        if (!schema.TryGetProperty("properties", out JsonElement properties))
        {
            page.Paragraph("Type: " + TypeOf(schema).Text + ".");
            return;
        }

        page.Table(["Property", "Type", "Required", "Null"], properties.EnumerateObject().Select(property =>
        {
            SchemaType type = TypeOf(property.Value);
            return (IReadOnlyList<string>)[MarkdownPage.Code(property.Name), type.Text, required.Contains(property.Name) ? "yes" : "no", type.Nullable ? "yes" : "no"];
        }));
    }

    /// <summary>
    /// A schema's type in a few words: a referenced schema as a link to its section, an array as <c>X[]</c>, a map as <c>map of X</c>, a
    /// number as its format (<c>int64</c>), anything else as its type; and whether it may be null.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns>The type.</returns>
    private static SchemaType TypeOf(JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            string name = reference.GetString()![_schemaReference.Length..];
            return new SchemaType($"[{name}](#{name.ToLowerInvariant()})", false);
        }

        if (schema.TryGetProperty("oneOf", out JsonElement oneOf) || schema.TryGetProperty("anyOf", out oneOf))
        {
            List<JsonElement> choices = [.. oneOf.EnumerateArray()];
            List<SchemaType> types = [.. choices.Where(choice => !IsNull(choice)).Select(TypeOf)];
            return new SchemaType(string.Join(" or ", types.Select(type => type.Text)), choices.Any(IsNull) || types.Any(type => type.Nullable));
        }

        List<string> kinds = schema.TryGetProperty("type", out JsonElement type)
            ? type.ValueKind == JsonValueKind.Array ? [.. type.EnumerateArray().Select(kind => kind.GetString()!)] : [type.GetString()!]
            : [];
        bool nullable = kinds.Remove("null");
        if (kinds.Contains("integer") || kinds.Contains("number"))
        {
            kinds.Remove("string");
        }

        string text = kinds switch
        {
            ["array"] => TypeOf(schema.GetProperty("items")).Text + "[]",
            ["object"] when schema.TryGetProperty("additionalProperties", out JsonElement values) && values.ValueKind == JsonValueKind.Object =>
                "map of " + TypeOf(values).Text,
            _ when schema.TryGetProperty("format", out JsonElement format) => MarkdownPage.Code(format.GetString()),
            [] => "any",
            _ => MarkdownPage.Code(string.Join(" or ", kinds)),
        };
        return new SchemaType(text, nullable);
    }

    /// <summary>Whether a schema is only <c>{ "type": "null" }</c>.</summary>
    /// <param name="schema">The schema.</param>
    /// <returns>True for the null schema.</returns>
    private static bool IsNull(JsonElement schema) =>
        schema.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String && type.GetString() == "null";

    /// <summary>One operation as the page shows it.</summary>
    /// <param name="Method">The HTTP method, upper case.</param>
    /// <param name="Path">The path.</param>
    /// <param name="Auth">The security schemes it needs, or "none".</param>
    /// <param name="Request">The request body's schema, or a dash.</param>
    /// <param name="Success">The 2xx responses with their schemas.</param>
    private sealed record Operation(string Method, string Path, string Auth, string Request, string Success);

    /// <summary>A schema's type as the page writes it.</summary>
    /// <param name="Text">The Markdown.</param>
    /// <param name="Nullable">Whether the value may be null.</param>
    private sealed record SchemaType(string Text, bool Nullable);
}
