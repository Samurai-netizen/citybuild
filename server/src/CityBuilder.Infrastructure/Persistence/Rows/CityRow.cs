namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>cities</c> (core-instructions/06-data-model.md): one player's city with its clock, era and active research. The
/// engine's <c>CityState</c> is mapped from it and its child rows (M0-20); this class is not a Simulation type.
/// </summary>
public sealed class CityRow
{
    /// <summary><c>id</c>, the primary key; set by the application, not the database.</summary>
    public Guid Id { get; set; }

    /// <summary><c>player_id</c>: the owner (<see cref="PlayerRow"/>); unique, one city per player.</summary>
    public Guid PlayerId { get; set; }

    /// <summary><c>era</c>: the city's era number, 1 or more.</summary>
    public short Era { get; set; }

    /// <summary><c>clock_seconds</c>: the city clock in seconds, the time the engine has advanced the city to.</summary>
    public long ClockSeconds { get; set; }

    /// <summary><c>last_settled_unix</c>: the server time (Unix seconds) of the last settlement.</summary>
    public long LastSettledUnix { get; set; }

    /// <summary><c>next_building_id</c>: the id the next placed building gets.</summary>
    public int NextBuildingId { get; set; }

    /// <summary><c>research_id</c>: the research being done, or <see langword="null"/> when none is active.</summary>
    public string? ResearchId { get; set; }

    /// <summary><c>research_started_at</c>: the city clock second the active research started, or <see langword="null"/>.</summary>
    public long? ResearchStartedAt { get; set; }

    /// <summary><c>research_ends_at</c>: the city clock second the active research completes, or <see langword="null"/>.</summary>
    public long? ResearchEndsAt { get; set; }

    /// <summary><c>state_version</c>: raised by every write; the concurrency token, a second guard after the row lock (06).</summary>
    public long StateVersion { get; set; }

    /// <summary><c>content_version</c>: the content version the city was last settled with.</summary>
    public string ContentVersion { get; set; } = string.Empty;

    /// <summary><c>created_at</c>: when the city was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary><c>updated_at</c>: when the city was last written (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
