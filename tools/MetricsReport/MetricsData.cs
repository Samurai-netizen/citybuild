namespace CityBuilder.MetricsReport;

/// <summary>
/// What the report computes from (docs/plan/CR-0009-T2-PLAN.md §3.1): the players of the population and their events, loaded once
/// and held in memory as plain records, so every number is computed and tested without a database.
/// </summary>
/// <param name="Players">The players of the population (created in the period, without the excluded ones).</param>
/// <param name="Events">Their events; events of a player not in <paramref name="Players"/> are ignored.</param>
public sealed record MetricsData(IReadOnlyList<MetricsPlayer> Players, IReadOnlyList<MetricsEvent> Events);

/// <summary>One player: an id only, never a name (the owner: "ids only").</summary>
/// <param name="Id">The player id.</param>
/// <param name="CreatedAt">When the player was created.</param>
/// <param name="LastSeenUnix">The city's last seen second (<c>cities.last_seen_unix</c>), which says whether the player left.</param>
public sealed record MetricsPlayer(Guid Id, DateTimeOffset CreatedAt, long LastSeenUnix);

/// <summary>One row of <c>player_events</c>, or a <c>production.first</c> event the loader made from the ledger.</summary>
/// <param name="Id">The event id; it orders events of the same second.</param>
/// <param name="PlayerId">The player it belongs to.</param>
/// <param name="EventType">The event type (<see cref="KnownEvents"/>, or any type recorded later).</param>
/// <param name="Detail">The content id it names, or <see langword="null"/>.</param>
/// <param name="Outcome"><c>accepted</c> or an error code for a command, else <see langword="null"/>.</param>
/// <param name="Value">A number it carries (a building level), or <see langword="null"/>.</param>
/// <param name="CityClock">The city clock when it happened.</param>
/// <param name="CreatedAt">The real time it was recorded.</param>
public sealed record MetricsEvent(
    long Id, Guid PlayerId, string EventType, string? Detail, string? Outcome, long? Value, long CityClock, DateTimeOffset CreatedAt);
