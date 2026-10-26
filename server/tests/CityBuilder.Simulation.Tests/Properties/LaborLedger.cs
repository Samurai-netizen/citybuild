using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Invariant 11 (<c>04</c> section 10, labor accounting): a cycle completes exactly when the worker-seconds put into it reach its labor. The
/// ledger counts them itself, step by step, from the workers each building had during the step, across every worker change, and never reads
/// the engine's <see cref="Building.LaborRemaining"/>. Per step:
/// <list type="bullet">
/// <item>a cycle still running has done less than its labor, and ends when the ledger says (<c>CeilDiv</c> of what is left by the workers
/// now), or never while it has no workers;</item>
/// <item>a cycle gone has done at least its labor, and on an event-aligned step not already a second earlier, unless an upgrade ended it;</item>
/// <item>a new cycle (on an aligned step) started at the step's end, with the recipe's labor less the research reductions completed by then.</item>
/// </list>
/// </summary>
internal sealed class LaborLedger
{
    /// <summary>The game content: recipes and research reductions.</summary>
    private readonly GameContent _content;
    /// <summary>The running cycle of each producing building, by building id, as the ledger counted it.</summary>
    private readonly Dictionary<int, Cycle> _cycles;

    /// <summary>An empty ledger for a new history on <paramref name="content"/>.</summary>
    public LaborLedger(GameContent content)
        : this(content, [])
    {
    }

    /// <summary>A ledger that goes on with the given cycles (<see cref="Fork"/>).</summary>
    private LaborLedger(GameContent content, Dictionary<int, Cycle> cycles)
    {
        _content = content;
        _cycles = cycles;
    }

    /// <summary>A ledger for a branch: the same cycles so far, kept apart from now on.</summary>
    public LaborLedger Fork() => new(_content, new Dictionary<int, Cycle>(_cycles));

    /// <summary>
    /// Checks one step and moves the ledger past it: adds the step's worker-seconds to every cycle that was running, then checks each cycle
    /// that continued, ended or started.
    /// </summary>
    /// <returns>The first failure, naming the building, or null when the labor of the step adds up.</returns>
    public string? Check(InvariantStep step, Action<string> count)
    {
        long seconds = step.After.Clock - step.Before.Clock;
        foreach (Building after in step.After.Buildings)
        {
            Building? before = step.Before.TryGetBuilding(after.Id, out Building? found) ? found : null;
            bool continues = false;
            if (before?.Phase == BuildingPhase.Producing)
            {
                if (!_cycles.TryGetValue(after.Id, out Cycle cycle) || cycle.StartedAt != before.PhaseStartedAt)
                {
                    return $"building {after.Id}: the ledger has no record of the cycle that started at {before.PhaseStartedAt}";
                }

                long work = cycle.Work + (before.AssignedWorkers * seconds);
                continues = after.Phase == BuildingPhase.Producing && after.PhaseStartedAt == cycle.StartedAt;
                string? problem = continues
                    ? Continued(after, cycle with { Work = work, Reassigned = cycle.Reassigned || before.AssignedWorkers != after.AssignedWorkers }, step, count)
                    : Ended(after.Id, cycle, work, before.AssignedWorkers, step, count);
                if (problem is not null)
                {
                    return $"building {after.Id}: {problem}";
                }
            }

            if (after.Phase == BuildingPhase.Producing && !continues)
            {
                string? problem = Started(after, step, count);
                if (problem is not null)
                {
                    return $"building {after.Id}: {problem}";
                }
            }
        }

        return null;
    }

    /// <summary>A cycle that runs on: it has done less than its labor, keeps that labor, and ends when the ledger says (never while it has no workers).</summary>
    private string? Continued(Building after, Cycle cycle, InvariantStep step, Action<string> count)
    {
        _cycles[after.Id] = cycle;
        long? endsAt = after.AssignedWorkers > 0 ? step.After.Clock + CeilDiv(cycle.Labor - cycle.Work, after.AssignedWorkers) : null;
        if (cycle.Work >= cycle.Labor)
        {
            return $"the cycle has had {cycle.Work} of its {cycle.Labor} worker-seconds and is still running";
        }

        if (after.CycleLabor != cycle.Labor || after.PhaseEndsAt != endsAt)
        {
            return $"after {cycle.Work} of {cycle.Labor} worker-seconds with {after.AssignedWorkers} workers the cycle must end at "
                + $"{End(endsAt)}; the building says labor {after.CycleLabor}, ends at {End(after.PhaseEndsAt)}";
        }

        if (after.AssignedWorkers == 0)
        {
            count("frozen cycles carried");
        }

        return null;
    }

    /// <summary>
    /// A cycle that is gone: an upgrade may end it at any time; otherwise it has done at least its labor, and on an event-aligned step it did
    /// not have it already a second earlier.
    /// </summary>
    private string? Ended(int id, Cycle cycle, long work, int workers, InvariantStep step, Action<string> count)
    {
        _cycles.Remove(id);
        if (step.Operation is CityOperation.UpgradeBuilding upgrade && upgrade.BuildingId == id && step.Mutation is not null)
        {
            count("cycles ended by an upgrade");
            return null;
        }

        count(cycle.Reassigned ? "cycles completed after a worker change" : "cycles completed with their first workers");
        if (work < cycle.Labor)
        {
            return $"the cycle ended after {work} of its {cycle.Labor} worker-seconds";
        }

        return step.EventAligned && work - workers >= cycle.Labor
            ? $"the cycle reached its {cycle.Labor} worker-seconds a second before it ended (at {work - workers} of them)"
            : null;
    }

    /// <summary>
    /// A new cycle: recorded with the worker-seconds since its start; on an event-aligned step it must start at the step's end, with the
    /// effective labor, and end when that labor gives.
    /// </summary>
    private string? Started(Building after, InvariantStep step, Action<string> count)
    {
        // Within a step every start happens at its end; a start inside the dormant tail is invariant 12's to report.
        long work = after.AssignedWorkers * (step.After.Clock - after.PhaseStartedAt);
        _cycles[after.Id] = new Cycle(after.PhaseStartedAt, after.CycleLabor, work, Reassigned: false);
        if (!step.EventAligned)
        {
            return null;
        }

        long labor = EffectiveLabor(step.After, after);
        if (labor < DormantTail.Definition(_content, after.TypeId).GetLevel(after.Level).Recipe!.Labor)
        {
            count("cycles started with a labor reduction");
        }

        long? endsAt = after.AssignedWorkers > 0 ? after.PhaseStartedAt + CeilDiv(labor, after.AssignedWorkers) : null;
        return after.PhaseStartedAt == step.After.Clock && after.CycleLabor == labor && after.PhaseEndsAt == endsAt
            ? null
            : $"a new cycle must start at {step.After.Clock} with labor {labor} and end at {End(endsAt)}; it started at "
                + $"{after.PhaseStartedAt} with labor {after.CycleLabor} and ends at {End(after.PhaseEndsAt)}";
    }

    /// <summary>The recipe's labor less the reductions of the completed research, rounded up (<c>04</c> section 3).</summary>
    private long EffectiveLabor(CityState city, Building building)
    {
        long labor = DormantTail.Definition(_content, building.TypeId).GetLevel(building.Level).Recipe!.Labor;
        long permille = city.CompletedResearch
            .SelectMany(id => _content.TryGetResearch(id, out ResearchDefinition? node) ? node.Effects : [])
            .OfType<LaborReductionEffect>()
            .Where(reduction => reduction.Building == building.TypeId)
            .Sum(reduction => (long)reduction.Permille);
        return CeilDiv(labor * (1000 - permille), 1000);
    }

    /// <summary>An end time as text, <c>never</c> for none.</summary>
    private static string End(long? endsAt) => endsAt.HasValue ? StructuralEquality.Text(endsAt.Value) : "never";

    /// <summary>Division rounded up, worked out here like the rest of the ledger, not with the engine's <see cref="IntegerMath.CeilDiv"/>.</summary>
    private static long CeilDiv(long dividend, long divisor) => (dividend + divisor - 1) / divisor;

    /// <summary>A running cycle as the ledger knows it.</summary>
    /// <param name="StartedAt">When the cycle started.</param>
    /// <param name="Labor">Its labor, in worker-seconds.</param>
    /// <param name="Work">The worker-seconds done so far.</param>
    /// <param name="Reassigned">Whether its workers ever changed.</param>
    private readonly record struct Cycle(long StartedAt, long Labor, long Work, bool Reassigned);
}
