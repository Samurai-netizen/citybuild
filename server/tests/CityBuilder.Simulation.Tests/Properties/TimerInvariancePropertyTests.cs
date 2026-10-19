using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Property 3, timer invariance (<c>04</c> section 10, invariant 3): constructions, upgrades and research complete at their end second however
/// the time is split, also beyond the production cap, where the pieces' production differs. Each city is taken right after a random command
/// that started a timer, and the time is cut into up to six pieces, some cuts on a timer's end second or next to it. Every timer must complete
/// in the piece that holds its end second, and afterwards the timer side of the city (levels, workers, buildings still under construction or
/// upgrade, research) must equal one advance over the whole time.
/// </summary>
public sealed class TimerInvariancePropertyTests
{
    // Most seeds must start a timer somewhere, or the property says little; today far more than this do (the failure prints the count).
    private const int _minimumSeedsWithTimers = 250;

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Advance_AnySplitAlsoBeyondTheCap_CompletesEveryTimerAtItsEndSecond(string contentName)
    {
        GameContent content = PropertyContent.Named(contentName);
        int seedsWithTimers = 0;
        PropertyCheck.ForEachSeed("timer invariance", contentName, (history, random) =>
        {
            int afterOperation = AfterATimerStarted(history, random);
            CityState whole = history.CopyAfter(content, afterOperation);
            List<(string Name, long EndsAt)> timers = Timers(whole);
            seedsWithTimers += timers.Count > 0 ? 1 : 0;

            long start = whole.Clock;
            long total = random.Between(0, 4 * content.Rules.MaxOfflineProductionSeconds);
            long[] cuts = Cuts(random, start, total, timers);
            SettlementReport wholeReport = Simulator.Advance(whole, content, total);

            CityState split = history.CopyAfter(content, afterOperation);
            var splitReports = new List<SettlementReport>();
            string context = $"after operation {afterOperation}, from clock {start} by {total} cut at [{string.Join(", ", cuts)}]";
            foreach (long cut in cuts.Append(start + total))
            {
                long pieceStart = split.Clock;
                SettlementReport report = Simulator.Advance(split, content, cut - pieceStart);
                splitReports.Add(report);
                string? misplaced = FirstMisplacedCompletion(timers, report, pieceStart, split.Clock, firstPiece: splitReports.Count == 1);
                if (misplaced is not null)
                {
                    return $"{context}: {misplaced}";
                }
            }

            string? difference = StructuralEquality.FirstDifference(TimerFields(whole), TimerFields(split))
                ?? StructuralEquality.FirstDifference(CompletionFields(ReportTotals.Sum(wholeReport)), CompletionFields(ReportTotals.Sum(splitReports)));
            return difference is null ? null : $"{context}: {difference}";
        });

        Assert.True(seedsWithTimers >= _minimumSeedsWithTimers, $"Only {seedsWithTimers} seeds had a running timer.");
    }

    // The number of operations to replay: up to a random command that started a timer (a placement, an upgrade or a research start), or the
    // whole history when there is none.
    private static int AfterATimerStarted(CityHistory history, SeededRandom random)
    {
        int[] candidates =
        [
            .. Enumerable.Range(1, history.Operations.Count)
                .Where(count => history.Operations[count - 1] is not (CityOperation.Advance or CityOperation.AssignWorkers)),
        ];
        return candidates.Length == 0 ? history.Operations.Count : random.Pick(candidates);
    }

    // The running timers by name ("building 3", "research x") and end second.
    private static List<(string Name, long EndsAt)> Timers(CityState city)
    {
        List<(string Name, long EndsAt)> timers =
        [
            .. city.Buildings
                .Where(building => building.Phase is BuildingPhase.Constructing or BuildingPhase.Upgrading)
                .Select(building => ($"building {building.Id}", building.PhaseEndsAt ?? throw new InvalidOperationException("A timer without an end."))),
        ];
        if (city.ActiveResearch is { } research)
        {
            timers.Add(($"research {research.Id}", research.EndsAt));
        }

        return timers;
    }

    // Up to five cut points between the start and the end, sorted; each is random, or a timer's end second, or one second before or after it.
    private static long[] Cuts(SeededRandom random, long start, long total, List<(string Name, long EndsAt)> timers)
    {
        long count = random.Between(0, 5);
        var cuts = new List<long>();
        for (long index = 0; index < count; index++)
        {
            long cut = timers.Count > 0 && random.Chance(50)
                ? random.Pick(timers).EndsAt + random.Between(-1, 1)
                : start + random.Between(0, total);
            cuts.Add(Math.Clamp(cut, start, start + total));
        }

        cuts.Sort();
        return [.. cuts];
    }

    // A timer must be reported by the piece whose window holds its end second: (pieceStart, pieceEnd], and the first piece also its start.
    private static string? FirstMisplacedCompletion(
        List<(string Name, long EndsAt)> timers, SettlementReport report, long pieceStart, long pieceEnd, bool firstPiece)
    {
        foreach ((string name, long endsAt) in timers)
        {
            bool due = endsAt <= pieceEnd && (endsAt > pieceStart || firstPiece);
            bool reported = name.StartsWith("building ", StringComparison.Ordinal)
                ? report.CompletedBuildings.Any(id => $"building {id}" == name)
                : report.CompletedResearch.Any(id => $"research {id}" == name);
            if (due != reported)
            {
                return $"{name} ends at {endsAt} but the piece {pieceStart}..{pieceEnd} {(reported ? "reported" : "did not report")} it";
            }
        }

        return null;
    }

    // What timers decide, and production doesn't: each building's level and workers (auto-staffing follows the houses' completions), which
    // buildings are still under construction or upgrade and until when, and the research.
    private static List<(string Path, string Value)> TimerFields(CityState city)
    {
        var fields = new List<(string Path, string Value)>();
        foreach (Building building in city.Buildings)
        {
            bool busy = building.Phase is BuildingPhase.Constructing or BuildingPhase.Upgrading;
            fields.Add(($"building {building.Id} level", StructuralEquality.Text(building.Level)));
            fields.Add(($"building {building.Id} assignedWorkers", StructuralEquality.Text(building.AssignedWorkers)));
            fields.Add(($"building {building.Id} timer", busy ? $"{building.Phase} until {building.PhaseEndsAt}" : "none"));
        }

        return [.. fields, .. StructuralEquality.Fields(city).Where(field => field.Path is "completedResearch" or "activeResearch")];
    }

    private static List<(string Path, string Value)> CompletionFields(ReportTotals totals) =>
        [.. totals.Fields().Where(field => field.Path is "completedBuildings" or "completedResearch")];
}
