using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CityBuilder.Application.Commands;

/// <summary>
/// The canonical payload of a command and its SHA-256 hash, stored in its receipt (<c>core-instructions/05-api-contract.md</c>, idempotency
/// rules): a retry with the same request id and the same hash is a replay, another hash a conflict. The payload is the command type, a line
/// break, and the command as JSON (System.Text.Json): its data properties, ordered by ordinal name so that reordering the code can't change
/// it, names as declared, numbers and text in the serializer's fixed format. The members of <see cref="ICityCommand"/> other than
/// <see cref="ICityCommand.RequestId"/> are not data and are left out. Changing this format changes every stored hash: a test pins it.
/// </summary>
public static class CommandPayload
{
    /// <summary>The names of the <see cref="ICityCommand"/> members that describe the command type rather than its data.</summary>
    private static readonly HashSet<string> _notData = typeof(ICityCommand).GetProperties()
        .Select(property => property.Name)
        .Where(name => name != nameof(ICityCommand.RequestId))
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>The serializer settings of the payload: the data properties only, in ordinal name order, no indentation.</summary>
    private static readonly JsonSerializerOptions _options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { DataPropertiesInOrdinalOrder } },
    };

    /// <summary>The canonical payload of the command, as text.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The command type, <c>\n</c>, and the command's data as JSON.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    public static string Canonical(ICityCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.CommandType + "\n" + JsonSerializer.Serialize(command, command.GetType(), _options);
    }

    /// <summary>The SHA-256 hash of the command's canonical payload, as UTF-8.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The hash, 32 bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    public static byte[] Hash(ICityCommand command) => SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(command)));

    /// <summary>
    /// Leaves out an object's properties that aren't data (<see cref="_notData"/>, on commands only) and orders the others by ordinal name.
    /// </summary>
    /// <param name="typeInfo">The serializer's contract of a type, as the default resolver made it.</param>
    private static void DataPropertiesInOrdinalOrder(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        bool isCommand = typeof(ICityCommand).IsAssignableFrom(typeInfo.Type);
        JsonPropertyInfo[] data = typeInfo.Properties
            .Where(property => !(isCommand && _notData.Contains(MemberName(property))))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        typeInfo.Properties.Clear();
        foreach (JsonPropertyInfo property in data)
        {
            typeInfo.Properties.Add(property);
        }
    }

    /// <summary>The name of the C# member behind a serialized property (its JSON name could differ).</summary>
    /// <param name="property">The serialized property.</param>
    /// <returns>The member's name.</returns>
    private static string MemberName(JsonPropertyInfo property) =>
        property.AttributeProvider is MemberInfo member ? member.Name : property.Name;
}
