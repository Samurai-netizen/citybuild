using System.Text;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Metrics;

/// <summary>
/// What a command's player event says (CR-0009, <c>command.&lt;type&gt;</c>): the content id a command is about, for
/// <see cref="Commands.ICityCommand.EventDetail"/>, and the outcome text of a rule's refusal. A detail is only ever an id the loaded content
/// (or the acting player's city) has, never the text the client sent, so no client-typed text reaches <c>player_events</c>
/// (<c>07</c>, logging policy).
/// </summary>
public static class CommandEvents
{
    /// <summary>The building type id when the content has that type, else <see langword="null"/>.</summary>
    /// <param name="content">The game content.</param>
    /// <param name="typeId">The id the command names.</param>
    /// <returns>The id, or <see langword="null"/> (also for text that is no id).</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public static string? BuildingType(GameContent content, string typeId)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(typeId);
        return ContentIds.IsValid(typeId) && content.TryGetBuilding(new BuildingTypeId(typeId), out _) ? typeId : null;
    }

    /// <summary>The type id of the city's building with the id, else <see langword="null"/> (a building the city doesn't have).</summary>
    /// <param name="city">The acting player's city, settled.</param>
    /// <param name="buildingId">The building's id in the city.</param>
    /// <returns>The building's type id, or <see langword="null"/>.</returns>
    public static string? BuildingTypeOf(CityState city, int buildingId)
    {
        ArgumentNullException.ThrowIfNull(city);
        return city.TryGetBuilding(buildingId, out Building? building) ? building.TypeId.Value : null;
    }

    /// <summary>The research id when the content has that node, else <see langword="null"/>.</summary>
    /// <param name="content">The game content.</param>
    /// <param name="researchId">The id the command names.</param>
    /// <returns>The id, or <see langword="null"/> (also for text that is no id).</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public static string? Research(GameContent content, string researchId)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(researchId);
        return ContentIds.IsValid(researchId) && content.TryGetResearch(new ResearchId(researchId), out _) ? researchId : null;
    }

    /// <summary>The item id when the content has that item, else <see langword="null"/>.</summary>
    /// <param name="content">The game content.</param>
    /// <param name="itemId">The id the command names.</param>
    /// <returns>The id, or <see langword="null"/> (also for text that is no id).</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public static string? Item(GameContent content, string itemId)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(itemId);
        return ContentIds.IsValid(itemId) && content.TryGetItem(new ItemId(itemId), out _) ? itemId : null;
    }

    /// <summary>
    /// The outcome of a refused command: the error code's spelling in the API (<c>05</c>, error catalogue), for example
    /// <c>INSUFFICIENT_RESOURCE</c>, the text the command log writes too.
    /// </summary>
    /// <param name="code">Why the engine refused the command.</param>
    /// <returns>The code in upper snake case.</returns>
    public static string Outcome(MutationErrorCode code)
    {
        string name = code.ToString();
        var outcome = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            // A capital starts a word: InsufficientResource -> INSUFFICIENT_RESOURCE.
            if (i > 0 && char.IsUpper(name[i]))
            {
                outcome.Append('_');
            }

            outcome.Append(char.ToUpperInvariant(name[i]));
        }

        return outcome.ToString();
    }
}
