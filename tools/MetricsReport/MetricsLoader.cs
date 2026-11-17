using System.Data;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CityBuilder.MetricsReport;

/// <summary>
/// Reads what the report computes from (docs/plan/CR-0009-T2-PLAN.md §3.1) in one read-only, repeatable-read transaction: the players of
/// the population with their creation and last seen, their events, and per city and item the first produced ledger row as a
/// <c>production.first</c> event. A dev name is used only in a <c>WHERE</c>, never selected (the owner: "ids only").
/// </summary>
public static class MetricsLoader
{
    /// <summary>The ledger reason of a settlement's outputs, the source of <c>production.first</c>.</summary>
    private const string _produced = "SETTLEMENT_PRODUCED";

    /// <summary>Loads the population and its events.</summary>
    /// <param name="db">A context on the database; the loader tracks nothing.</param>
    /// <param name="filter">The period and the excluded dev names.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The data.</returns>
    /// <exception cref="MetricsDatabaseException">The database has no <c>player_events</c> table.</exception>
    public static Task<MetricsData> LoadAsync(CityBuilderDbContext db, LoadFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(filter);
        return ReadOnlyAsync(db, token => ReadAsync(db, filter, token), cancellationToken);
    }

    /// <summary>Runs some work in one transaction that is read-only and repeatable read, then rolls it back.</summary>
    /// <typeparam name="T">The work's result.</typeparam>
    /// <param name="db">The context the work uses.</param>
    /// <param name="work">The work.</param>
    /// <param name="cancellationToken">Cancels it.</param>
    /// <returns>The work's result.</returns>
    public static async Task<T> ReadOnlyAsync<T>(CityBuilderDbContext db, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(work);
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        T result = await work(cancellationToken);
        await transaction.RollbackAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Turns the first produced ledger row per player and item into <c>production.first</c> events: id 0 (so it comes before a recorded
    /// event of the same second), the city clock of the player's last recorded event at or before it (0 without one; the ledger keeps
    /// no city clock), in player, time and item order.
    /// </summary>
    /// <param name="firsts">Per player and item the time of the first produced row.</param>
    /// <param name="events">The player's recorded events.</param>
    /// <returns>The events.</returns>
    public static IReadOnlyList<MetricsEvent> ProductionFirst(
        IEnumerable<(Guid PlayerId, string ItemId, DateTimeOffset At)> firsts, IReadOnlyList<MetricsEvent> events)
    {
        ArgumentNullException.ThrowIfNull(firsts);
        ArgumentNullException.ThrowIfNull(events);
        ILookup<Guid, MetricsEvent> byPlayer = events.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToLookup(e => e.PlayerId);
        return [.. firsts
            .OrderBy(f => f.PlayerId)
            .ThenBy(f => f.At)
            .ThenBy(f => f.ItemId, StringComparer.Ordinal)
            .Select(f => new MetricsEvent(
                0, f.PlayerId, KnownEvents.ProductionFirst, f.ItemId, null, null,
                byPlayer[f.PlayerId].LastOrDefault(e => e.CreatedAt <= f.At)?.CityClock ?? 0, f.At))];
    }

    /// <summary>The queries, inside the read-only transaction.</summary>
    /// <param name="db">The context.</param>
    /// <param name="filter">The period and the excluded dev names.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The data.</returns>
    /// <exception cref="MetricsDatabaseException">The database has no <c>player_events</c> table.</exception>
    private static async Task<MetricsData> ReadAsync(CityBuilderDbContext db, LoadFilter filter, CancellationToken cancellationToken)
    {
        bool hasEvents = await db.Database
            .SqlQueryRaw<bool>("SELECT to_regclass('player_events') IS NOT NULL AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (!hasEvents)
        {
            throw new MetricsDatabaseException(
                "The database has no player_events table: run the server once on it (it applies the migrations) and try again.");
        }

        IQueryable<PlayerRow> population = Population(db, filter);
        IQueryable<Guid> ids = population.Select(p => p.Id);

        var players = await population
            .Select(p => new { p.Id, p.CreatedAt, LastSeen = db.Cities.Where(c => c.PlayerId == p.Id).Max(c => (long?)c.LastSeenUnix) })
            .ToListAsync(cancellationToken);
        List<MetricsEvent> events = await db.PlayerEvents
            .AsNoTracking()
            .Where(e => ids.Contains(e.PlayerId))
            .OrderBy(e => e.Id)
            .Select(e => new MetricsEvent(e.Id, e.PlayerId, e.EventType, e.Detail, e.Outcome, e.Value, e.CityClock, e.CreatedAt))
            .ToListAsync(cancellationToken);
        var firsts = await (
                from entry in db.LedgerEntries.AsNoTracking()
                join city in db.Cities.AsNoTracking() on entry.CityId equals city.Id
                where entry.Reason == _produced && ids.Contains(city.PlayerId)
                group entry.CreatedAt by new { city.PlayerId, entry.ItemId } into item
                select new { item.Key.PlayerId, item.Key.ItemId, At = item.Min() })
            .ToListAsync(cancellationToken);

        return new MetricsData(
            [.. players.Select(p => new MetricsPlayer(p.Id, p.CreatedAt, p.LastSeen ?? p.CreatedAt.ToUnixTimeSeconds()))],
            [.. events, .. ProductionFirst(firsts.Select(f => (f.PlayerId, f.ItemId, f.At)), events)]);
    }

    /// <summary>The players created in the period (UTC days, both ends included) whose dev name is not excluded.</summary>
    /// <param name="db">The context.</param>
    /// <param name="filter">The period and the excluded dev names.</param>
    /// <returns>The query; it selects nothing yet.</returns>
    private static IQueryable<PlayerRow> Population(CityBuilderDbContext db, LoadFilter filter)
    {
        IQueryable<PlayerRow> players = db.Players.AsNoTracking();
        if (filter.From is DateOnly from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            players = players.Where(p => p.CreatedAt >= start);
        }

        if (filter.To is DateOnly to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            players = players.Where(p => p.CreatedAt < end);
        }

        if (filter.Exclude.Count > 0)
        {
            string[] excluded = [.. filter.Exclude];
            players = players.Where(p => !excluded.Contains(p.DevName));
        }

        return players;
    }
}

/// <summary>Which players the loader reads (docs/plan/CR-0009-T2-PLAN.md §3.3, population).</summary>
/// <param name="From">The first creation day (UTC), or none.</param>
/// <param name="To">The last creation day (UTC), or none.</param>
/// <param name="Exclude">The dev names whose players don't count.</param>
public sealed record LoadFilter(DateOnly? From, DateOnly? To, IReadOnlyList<string> Exclude);

/// <summary>The database can't give the report what it needs (for example, it has no <c>player_events</c> table).</summary>
public sealed class MetricsDatabaseException : Exception
{
    /// <summary>Makes the error with no message.</summary>
    public MetricsDatabaseException()
    {
    }

    /// <summary>Makes the error.</summary>
    /// <param name="message">What is wrong, for the developer.</param>
    public MetricsDatabaseException(string message)
        : base(message)
    {
    }

    /// <summary>Makes the error with its cause.</summary>
    /// <param name="message">What is wrong, for the developer.</param>
    /// <param name="innerException">The cause.</param>
    public MetricsDatabaseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
