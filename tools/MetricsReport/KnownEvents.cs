using System.Collections.Frozen;
using System.Reflection;
using CityBuilder.Application.Commands;
using CityBuilder.Domain.Metrics;

namespace CityBuilder.MetricsReport;

/// <summary>
/// The event types a checkpoint may match (docs/plan/CR-0009-T2-PLAN.md §3.2): <c>command.</c> and a command's wire type, the
/// names the settlement and the session record (<see cref="PlayerEventTypes"/>), and <see cref="ProductionFirst"/>, which the
/// report reads from the ledger.
/// </summary>
public static class KnownEvents
{
    /// <summary>The first production of an item in a city: not a recorded event, the report takes it from the first ledger row (CR-0009-T1 plan).</summary>
    public const string ProductionFirst = "production.first";

    /// <summary>The names that are not commands: what the settlement and the session record, and the first production.</summary>
    private static readonly FrozenSet<string> _named = new[]
    {
        PlayerEventTypes.ResearchCompleted, PlayerEventTypes.BuildingCompleted, PlayerEventTypes.SessionStart,
        PlayerEventTypes.PlayerCreated, ProductionFirst,
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The wire type of every command, found in the Application assembly, so a command added by the recipe is known with no change here.</summary>
    public static FrozenSet<string> CommandTypes { get; } = FindCommandTypes();

    /// <summary>Whether a checkpoint may match an event type.</summary>
    /// <param name="eventType">The event type as the checkpoint file names it.</param>
    /// <returns><see langword="true"/> for a known command's event type or one of the other known names.</returns>
    public static bool IsKnown(string eventType) =>
        _named.Contains(eventType)
        || (eventType.StartsWith(PlayerEventTypes.CommandPrefix, StringComparison.Ordinal)
            && CommandTypes.Contains(eventType[PlayerEventTypes.CommandPrefix.Length..]));

    /// <summary>Reads the <c>Type</c> constant of every command class (each command declares its wire type there, <c>docs/how-to/add-command.md</c>).</summary>
    /// <returns>The wire types.</returns>
    /// <exception cref="InvalidOperationException">A command class has no public <c>Type</c> text constant.</exception>
    private static FrozenSet<string> FindCommandTypes()
    {
        var types = new List<string>();
        foreach (Type command in typeof(ICityCommand).Assembly.GetTypes()
                     .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ICityCommand).IsAssignableFrom(type)))
        {
            FieldInfo? field = command.GetField("Type", BindingFlags.Public | BindingFlags.Static);
            if (field is not { IsLiteral: true } || field.GetRawConstantValue() is not string wireType)
            {
                throw new InvalidOperationException($"The command {command.Name} has no public const string Type (its wire type).");
            }

            types.Add(wireType);
        }

        return types.ToFrozenSet(StringComparer.Ordinal);
    }
}
