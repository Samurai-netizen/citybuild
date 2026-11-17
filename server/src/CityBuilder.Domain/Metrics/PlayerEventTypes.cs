namespace CityBuilder.Domain.Metrics;

/// <summary>
/// The names of the player event types and the command outcome "accepted" (CR-0009, <c>core-instructions/06-data-model.md</c>,
/// <c>player_events</c>). The report's checkpoints (<c>server/analytics/checkpoints.json</c>, CR-0009-T2) match these names, so a
/// rename splits the history in two.
/// </summary>
public static class PlayerEventTypes
{
    /// <summary>The prefix of a command's event type: <c>command.</c> and the command's wire type (<see cref="ForCommand"/>).</summary>
    public const string CommandPrefix = "command.";

    /// <summary>A settlement completed a research; the detail is the research id.</summary>
    public const string ResearchCompleted = "research.completed";

    /// <summary>A settlement completed a construction or an upgrade; the detail is the building type, the value the level it now has.</summary>
    public const string BuildingCompleted = "building.completed";

    /// <summary>The player came back: the first login, or a settling request later than the session gap after the last seen second.</summary>
    public const string SessionStart = "session.start";

    /// <summary>The first login created the player and the city.</summary>
    public const string PlayerCreated = "player.created";

    /// <summary>The outcome of a command that was applied; a refused one carries its error code instead.</summary>
    public const string OutcomeAccepted = "accepted";

    /// <summary>The event type of a command: <see cref="CommandPrefix"/> and its wire type, for example <c>command.place-building</c>.</summary>
    /// <param name="commandType">The command's wire type (<c>ICityCommand.CommandType</c>); not blank.</param>
    /// <returns>The event type.</returns>
    /// <exception cref="ArgumentException">The command type is blank.</exception>
    public static string ForCommand(string commandType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);
        return CommandPrefix + commandType;
    }
}
