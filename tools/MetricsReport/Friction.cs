using CityBuilder.Domain.Metrics;

namespace CityBuilder.MetricsReport;

/// <summary>
/// The friction section (docs/plan/CR-0009-T2-PLAN.md §3.4): refused commands by command and error code, the last event of each player
/// who left, and the players who left with a refusal in their last session.
/// </summary>
public static class Friction
{
    /// <summary>How many groups the "last event of each player who left" list holds.</summary>
    public const int LastEventCount = 10;

    /// <summary>Builds the section.</summary>
    /// <param name="context">The population, who left, the least sample.</param>
    /// <returns>The three lists.</returns>
    public static FrictionReport Build(ReportContext context)
    {
        PlayerTimeline[] leavers = [.. context.Timelines.Where(t => context.HasLeft(t.Player))];
        DetailCount[] lastEvents = [.. leavers
            .Where(t => t.Events.Count > 0)
            .GroupBy(t => ReportCheckpoint.Unnamed(t.Events[^1].EventType, t.Events[^1].Detail, t.Events[^1].Outcome).Title, StringComparer.Ordinal)
            .Select(g => (Title: g.Key, Players: g.Count()))
            .OrderByDescending(g => g.Players)
            .ThenBy(g => g.Title, StringComparer.Ordinal)
            .Take(LastEventCount)
            .Select(g => new DetailCount(g.Title, g.Players, context.Percent(g.Players, context.LeftCount)))];

        return new FrictionReport(
            Refusals(context, context.Timelines.Select(t => t.Events), context.Population),
            lastEvents,
            Refusals(context, leavers.Select(LastSession), context.LeftCount));
    }

    /// <summary>Whether an event is a command refused by a game rule.</summary>
    /// <param name="playerEvent">The event.</param>
    /// <returns><see langword="true"/> for a command event with an error code.</returns>
    private static bool IsRefusal(MetricsEvent playerEvent) =>
        playerEvent.EventType.StartsWith(PlayerEventTypes.CommandPrefix, StringComparison.Ordinal)
        && playerEvent.Outcome is not null
        && playerEvent.Outcome != PlayerEventTypes.OutcomeAccepted;

    /// <summary>A player's last session: the events from their last <c>session.start</c> on, or all of them without one.</summary>
    /// <param name="timeline">The player's timeline.</param>
    /// <returns>The events of the last session.</returns>
    private static IEnumerable<MetricsEvent> LastSession(PlayerTimeline timeline)
    {
        int start = 0;
        for (int index = timeline.Events.Count - 1; index >= 0; index--)
        {
            if (timeline.Events[index].EventType == PlayerEventTypes.SessionStart)
            {
                start = index;
                break;
            }
        }

        return timeline.Events.Skip(start);
    }

    /// <summary>The refusals in some events of some players, by command and code: the most players first, then the most times.</summary>
    /// <param name="context">The report's context.</param>
    /// <param name="eventsPerPlayer">Per player, the events to read.</param>
    /// <param name="whole">The players the shares are of.</param>
    /// <returns>The rows.</returns>
    private static FrictionRow[] Refusals(ReportContext context, IEnumerable<IEnumerable<MetricsEvent>> eventsPerPlayer, int whole) =>
        [.. eventsPerPlayer
            .SelectMany(events => events
                .Where(IsRefusal)
                .GroupBy(e => (e.EventType, Outcome: e.Outcome!))
                .Select(g => (g.Key.EventType, g.Key.Outcome, Times: (long)g.Count())))
            .GroupBy(p => (p.EventType, p.Outcome))
            .Select(g => new FrictionRow(g.Key.EventType, g.Key.Outcome, g.Count(), context.Percent(g.Count(), whole), g.Sum(p => p.Times)))
            .OrderByDescending(r => r.Players)
            .ThenByDescending(r => r.Times)
            .ThenBy(r => r.EventType, StringComparer.Ordinal)
            .ThenBy(r => r.Outcome, StringComparer.Ordinal)];
}

/// <summary>The friction section's three lists.</summary>
/// <param name="Refusals">Refused commands by command and code; shares of the population.</param>
/// <param name="LastEvents">The last event of each player who left, grouped, at most <see cref="Friction.LastEventCount"/>; shares of those who left.</param>
/// <param name="LastSessionRefusals">The refusals in the last session of the players who left; shares of those who left.</param>
public sealed record FrictionReport(
    IReadOnlyList<FrictionRow> Refusals, IReadOnlyList<DetailCount> LastEvents, IReadOnlyList<FrictionRow> LastSessionRefusals);

/// <summary>One command and error code.</summary>
/// <param name="EventType">The command's event type.</param>
/// <param name="Outcome">The error code.</param>
/// <param name="Players">The players refused this way.</param>
/// <param name="Share">Their share of the list's players.</param>
/// <param name="Times">The refusals in total.</param>
public sealed record FrictionRow(string EventType, string Outcome, int Players, Rate Share, long Times);
