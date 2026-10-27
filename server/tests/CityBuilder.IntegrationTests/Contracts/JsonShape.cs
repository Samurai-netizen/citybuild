using System.Text.Json;
using System.Text.Json.Nodes;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// Checks the shape of a raw JSON object against the contract (<c>core-instructions/05-api-contract.md</c>): exactly these property names,
/// each with the JSON type the contract gives it, and a null only where the contract allows one. It looks at the text the server sent, so
/// a field that is renamed, missing, extra, a string instead of a number, or an omitted null fails here, whatever the C# type says.
/// </summary>
internal static class JsonShape
{
    /// <summary>
    /// Asserts that <paramref name="node"/> is an object with exactly the properties of <paramref name="shape"/>.
    /// </summary>
    /// <param name="node">The JSON value under test.</param>
    /// <param name="shape">The properties, separated by spaces, each <c>name:type</c> with the type <c>string</c>, <c>int</c> (a number with no
    /// fraction or exponent), <c>bool</c>, <c>object</c> or <c>array</c>; a trailing <c>?</c> allows <see langword="null"/>. A nullable property
    /// must still be present: the contract writes its nulls.</param>
    /// <param name="path">Where the value is in the document, for the failure message, for example <c>buildings[2].levels[0]</c>.</param>
    public static void AssertObject(JsonNode? node, string shape, string path)
    {
        JsonObject actual = Assert.IsType<JsonObject>(node);
        (string Name, string Type, bool Nullable)[] expected = shape
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(field =>
            {
                string[] parts = field.Split(':');
                return (parts[0], parts[1].TrimEnd('?'), parts[1].EndsWith('?'));
            })
            .ToArray();

        // The path is part of both strings, so a failure says where the names differ.
        Assert.Equal(
            $"{path}: {string.Join(' ', expected.Select(field => field.Name).Order(StringComparer.Ordinal))}",
            $"{path}: {string.Join(' ', actual.Select(property => property.Key).Order(StringComparer.Ordinal))}");

        foreach ((string name, string type, bool nullable) in expected)
        {
            JsonNode? value = actual[name];
            if (value is null)
            {
                Assert.True(nullable, $"{path}.{name} is null, but the contract does not allow it.");
                continue;
            }

            Assert.True(IsOfType(value, type), $"{path}.{name} should be {type} but is {value.ToJsonString()}.");
        }
    }

    /// <summary>Whether the value has the JSON type the shape names.</summary>
    /// <param name="value">A value that is not null.</param>
    /// <param name="type">One of the types <see cref="AssertObject"/> accepts.</param>
    /// <returns><see langword="true"/> when it has the type.</returns>
    private static bool IsOfType(JsonNode value, string type) => type switch
    {
        "string" => value.GetValueKind() == JsonValueKind.String,
        "int" => value.GetValueKind() == JsonValueKind.Number && value.ToJsonString().All(character => char.IsAsciiDigit(character) || character == '-'),
        "bool" => value.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
        "object" => value.GetValueKind() == JsonValueKind.Object,
        "array" => value.GetValueKind() == JsonValueKind.Array,
        _ => throw new ArgumentException($"Unknown shape type '{type}'.", nameof(type)),
    };
}
