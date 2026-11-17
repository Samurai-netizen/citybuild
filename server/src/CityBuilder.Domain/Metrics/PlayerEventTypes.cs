namespace CityBuilder.Domain.Metrics;

/// <summary>The names of the player event types (CR-0009).</summary>
public static class PlayerEventTypes
{
    /// <summary>Research completed.</summary>
    public const string ResearchCompleted = "";

    /// <summary>Building completed.</summary>
    public const string BuildingCompleted = "";

    /// <summary>Session start.</summary>
    public const string SessionStart = "";

    /// <summary>Player created.</summary>
    public const string PlayerCreated = "";

    /// <summary>The event type of a command.</summary>
    /// <param name="commandType">The command's wire type.</param>
    /// <returns>The event type.</returns>
    public static string ForCommand(string commandType) => throw new NotImplementedException();
}
