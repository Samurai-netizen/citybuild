using System.Globalization;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Replays a generated history step by step and hands every step to the checks (<see cref="InvariantStep"/>), so the invariants hold between
/// any two events, not only between the generated operations:
/// <list type="bullet">
/// <item>a command is one step: the mutation and its zero-length advance;</item>
/// <item>an advance within the production cap is a zero-length step, then one step to each next due time (a timer's end), so every start
/// and completion falls on a step's end. Split invariance (property 2) makes this the same city as one advance;</item>
/// <item>an advance beyond the cap runs whole on the city. A copy is stepped like above to the end of the window, and the rest, the dormant
/// tail, is one step from the copy to the city, with what <see cref="DormantTail"/> says it must do;</item>
/// <item>after some steps a <b>branch</b> leaves the history: on a copy, a random command (mostly an upgrade or a worker change on a running
/// cycle, at whatever second the step ended), then a short stepped advance, checked with a fork of the checks. Branches never change the
/// replayed city, so the history stays valid.</item>
/// </list>
/// After the history, a copy is advanced by a negative time. The observer throws when its steps don't add up to the generated reports and
/// city: then it did not observe the history it was given.
/// </summary>
internal sealed class InvariantObserver
{
    /// <summary>The chance in percent that a branch leaves the history after one of its steps.</summary>
    private const int _branchPercent = 5;

    /// <summary>The game content the history runs on.</summary>
    private readonly GameContent _content;
    /// <summary>The observer's own random numbers: where branches leave, what they do, and the negative advance at the end.</summary>
    private readonly SeededRandom _random;
    /// <summary>The production cap in seconds (<see cref="GameRules.MaxOfflineProductionSeconds"/>): a longer advance has a dormant tail.</summary>
    private readonly long _cap;

    /// <summary>An observer of one history on <paramref name="content"/>, drawing its own choices from <paramref name="random"/>.</summary>
    private InvariantObserver(GameContent content, SeededRandom random)
    {
        _content = content;
        _random = random;
        _cap = content.Rules.MaxOfflineProductionSeconds;
    }

    /// <summary>
    /// Replays the history from a new starter city step by step and hands every step to <paramref name="checks"/>. Throws when the observed
    /// steps don't add up to the generated reports and city.
    /// </summary>
    public static void Observe(GameContent content, CityHistory history, SeededRandom random, InvariantChecks checks) =>
        new InvariantObserver(content, random).Run(history, new Line(checks, Branch: null));

    /// <summary>
    /// Replays every operation, compares each operation's reports and then the final city with the generated history, and last checks a
    /// negative advance on a copy.
    /// </summary>
    private void Run(CityHistory history, Line line)
    {
        CityState city = CityFactory.CreateStarterCity(_content);
        for (int index = 0; index < history.Operations.Count; index++)
        {
            CityOperation operation = history.Operations[index];
            List<SettlementReport> reports = operation switch
            {
                CityOperation.Command command => [Command(city, command, index + 1, line)!],
                CityOperation.Advance advance when advance.Seconds <= _cap => Stepped(city, advance, index + 1, advance.Seconds, city.Clock, line),
                CityOperation.Advance advance => [Dormant(city, advance, index + 1, line)],
                _ => throw new InvalidOperationException($"Unknown operation {operation}."),
            };
            Same($"operation {index + 1}'s report", ReportTotals.Sum(history.Reports[index]).Fields(), ReportTotals.Sum(reports).Fields());
        }

        Same("the final city", StructuralEquality.Fields(history.City), StructuralEquality.Fields(city));

        // A negative time counts as 0 (04 section 1): the clock stays.
        var backwards = new CityOperation.Advance(-_random.Between(1, _cap));
        Piece(CityCopy.Of(city), backwards, 0, backwards.Seconds, city.Clock, line with { Branch = "after the history, on a copy" });
    }

    /// <summary>
    /// The command's step: the mutation and its zero-length advance, checked as one step. Null when a branch's command was rejected (a
    /// rejection changes nothing: the rule tests' business); a rejection on the history itself throws, since the replay no longer follows it.
    /// </summary>
    private SettlementReport? Command(CityState city, CityOperation.Command command, int number, Line line)
    {
        CityState before = CityCopy.Of(city);
        MutationResult result = command.Mutate(city, _content);
        if (!result.IsOk)
        {
            return line.Branch is not null ? null : throw new InvalidOperationException($"Operation {number} ({command}) was rejected on replay: {result}.");
        }

        SettlementReport report = Simulator.Advance(city, _content, 0);
        line.Checks.Check(Step(before, city, command, number, result, requested: 0, report, city.Clock, line));
        return report;
    }

    /// <summary>
    /// An advance in steps: a zero-length piece first (after a dormant tail the city may have idle producers that can start), then one piece
    /// to each next due time, until <paramref name="seconds"/> have passed.
    /// </summary>
    private List<SettlementReport> Stepped(CityState city, CityOperation operation, int number, long seconds, long windowStart, Line line)
    {
        long target = city.Clock + seconds;
        var reports = new List<SettlementReport> { Piece(city, operation, number, 0, windowStart, line) };
        while (city.Clock < target)
        {
            long next = NextDueAt(city) ?? target;
            if (next <= city.Clock)
            {
                throw new InvalidOperationException($"After operation {number} a timer is due at {next}, not after the clock {city.Clock}.");
            }

            reports.Add(Piece(city, operation, number, Math.Min(next, target) - city.Clock, windowStart, line));
        }

        return reports;
    }

    /// <summary>One piece of an advance, checked as a step; after a step of the history itself a branch may leave (<see cref="_branchPercent"/>).</summary>
    private SettlementReport Piece(CityState city, CityOperation operation, int number, long seconds, long windowStart, Line line)
    {
        CityState before = CityCopy.Of(city);
        SettlementReport report = Simulator.Advance(city, _content, seconds);
        line.Checks.Check(Step(before, city, operation, number, mutation: null, requested: seconds, report, windowStart, line));
        if (line.Branch is null && _random.Chance(_branchPercent))
        {
            BranchFrom(city, number, line);
        }

        return report;
    }

    /// <summary>
    /// Leaves the history on a copy of the city: an aimed command and, when it is applied, a stepped advance of 0 to 600 seconds (never more
    /// than the cap), checked with a fork of the checks. The replayed city does not change.
    /// </summary>
    private void BranchFrom(CityState city, int number, Line line)
    {
        CityState branch = CityCopy.Of(city);
        CityOperation.Command? command = AimedCommand(branch);
        long gap = _random.Between(0, Math.Min(600, _cap));
        if (command is null)
        {
            return;
        }

        var branchLine = new Line(
            line.Checks.Fork(),
            string.Create(CultureInfo.InvariantCulture, $"branch at clock {city.Clock}: {command}, then Advance {gap}"));
        if (Command(branch, command, number, branchLine) is not null)
        {
            Stepped(branch, new CityOperation.Advance(gap), number, gap, branch.Clock, branchLine);
        }
    }

    /// <summary>
    /// The branch's command: mostly a worker change (a new labor segment) or an upgrade on a producing building, an upgrade preferably where
    /// the cycle took inputs (it refunds them); else any command, or null when <see cref="CityGenerator.RandomCommand"/> gives none.
    /// </summary>
    private CityOperation.Command? AimedCommand(CityState city)
    {
        List<Building> producing = [.. city.Buildings.Where(building => building.Phase == BuildingPhase.Producing)];
        if (producing.Count == 0 || _random.Chance(20))
        {
            return CityGenerator.RandomCommand(_random, city, _content);
        }

        if (_random.Chance(50))
        {
            Building building = _random.Pick(producing);
            return new CityOperation.AssignWorkers(building.Id, (int)_random.Between(0, Level(building).MaxWorkers));
        }

        List<Building> withInputs = [.. producing.Where(building => Level(building).Recipe!.Inputs.Count > 0)];
        return new CityOperation.UpgradeBuilding(_random.Pick(withInputs.Count > 0 ? withInputs : producing).Id);
    }

    /// <summary>The definition of a building's current level.</summary>
    private BuildingLevelDefinition Level(Building building) => DormantTail.Definition(_content, building.TypeId).GetLevel(building.Level);

    /// <summary>
    /// An advance beyond the cap: the city advances in one call, a copy is stepped through the window, and the rest, from the copy to the
    /// city, is checked as one dormant-tail step against <see cref="DormantTail.Model"/>.
    /// </summary>
    private SettlementReport Dormant(CityState city, CityOperation.Advance advance, int number, Line line)
    {
        long windowStart = city.Clock;
        CityState window = CityCopy.Of(city);
        SettlementReport whole = Simulator.Advance(city, _content, advance.Seconds);

        List<SettlementReport> prefix = Stepped(window, advance, number, _cap, windowStart, line);
        line.Checks.Check(new InvariantStep
        {
            Before = window,
            After = city,
            Operation = advance,
            OperationNumber = number,
            Requested = advance.Seconds - _cap,
            ReportedElapsed = whole.ElapsedSeconds - prefix.Sum(report => report.ElapsedSeconds),
            Produced = Minus(whole.Produced, prefix.Select(report => report.Produced)),
            Consumed = Minus(whole.Consumed, prefix.Select(report => report.Consumed)),
            EventAligned = false,
            ProductionUntil = windowStart + _cap,
            ExpectedTail = DormantTail.Model(_content, window, city.Clock),
            CompletedBuildings = [.. whole.CompletedBuildings.Except(prefix.SelectMany(report => report.CompletedBuildings))],
            CompletedResearch = [.. whole.CompletedResearch.Except(prefix.SelectMany(report => report.CompletedResearch))],
        });
        return whole;
    }

    /// <summary>The step from <paramref name="before"/> to <paramref name="after"/>, with what the report says happened in between.</summary>
    private InvariantStep Step(
        CityState before, CityState after, CityOperation operation, int number, MutationResult? mutation, long requested, SettlementReport report,
        long windowStart, Line line) => new()
        {
            Before = before,
            After = after,
            Operation = operation,
            OperationNumber = number,
            Branch = line.Branch,
            Mutation = mutation,
            Requested = requested,
            ReportedElapsed = report.ElapsedSeconds,
            Produced = report.Produced,
            Consumed = report.Consumed,
            EventAligned = true,
            ProductionUntil = windowStart + _cap,
            CompletedBuildings = report.CompletedBuildings,
            CompletedResearch = report.CompletedResearch,
        };

    /// <summary>
    /// The earliest timer end of the city, read from its public state: research, constructions, upgrades and running cycles. Null when no
    /// timer runs.
    /// </summary>
    private static long? NextDueAt(CityState city)
    {
        long? next = city.ActiveResearch?.EndsAt;
        foreach (Building building in city.Buildings)
        {
            if (building.PhaseEndsAt < (next ?? long.MaxValue))
            {
                next = building.PhaseEndsAt;
            }
        }

        return next;
    }

    /// <summary>The quantities of the whole advance less those of its stepped pieces: what the dormant tail did.</summary>
    private static SortedDictionary<ItemId, long> Minus(IReadOnlyDictionary<ItemId, long> whole, IEnumerable<IReadOnlyDictionary<ItemId, long>> parts)
    {
        var rest = new SortedDictionary<ItemId, long>(whole.ToDictionary(item => item.Key, item => item.Value));
        foreach (KeyValuePair<ItemId, long> part in parts.SelectMany(quantities => quantities))
        {
            rest[part.Key] = rest.GetValueOrDefault(part.Key) - part.Value;
        }

        return rest;
    }

    /// <summary>Throws when the observed values differ from the generated ones: then the observer did not replay the history it was given.</summary>
    private static void Same(string what, IReadOnlyList<(string Path, string Value)> generated, IReadOnlyList<(string Path, string Value)> observed)
    {
        string? difference = StructuralEquality.FirstDifference(generated, observed);
        if (difference is not null)
        {
            throw new InvalidOperationException($"The observed steps differ from the generated history in {what}: {difference}");
        }
    }

    /// <summary>Where steps go: the history's own checks, or a branch's fork of them with a description of how the branch left.</summary>
    /// <param name="Checks">The checks the steps go to.</param>
    /// <param name="Branch">Null for the history itself; for a branch, how it left the history.</param>
    private sealed record Line(InvariantChecks Checks, string? Branch);
}
