using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The invariants of <c>04</c> section 10, checked on every observed step of one history (<see cref="InvariantObserver"/>). Each check reads
/// only public state, content and what the engine reported, and computes what it needs (reservations, caps, workers) itself, so a wrong
/// engine query can't hide a wrong state. A failure goes to the callback with the invariant; counts of the cases each check really
/// exercised go to the other one, so the coverage test can tell a check that never fires from one that holds.
/// </summary>
internal sealed class InvariantChecks
{
    private readonly GameContent _content;
    private readonly Action<Invariant, string> _fail;
    private readonly Action<string> _count;

    public InvariantChecks(GameContent content, Action<Invariant, string> fail, Action<string> count)
    {
        _content = content;
        _fail = fail;
        _count = count;
    }

    /// <summary>Checks for a branch that leaves the history here: the same callbacks, and a copy of what these checks track so far.</summary>
    public InvariantChecks Fork() => new(_content, _fail, _count);

    public void Check(InvariantStep step)
    {
        _count("steps");
        Report(Invariant.Conservation, step, Conservation(step));
        Report(Invariant.NoNegativeStock, step, NoNegativeStock(step.After));
        Report(Invariant.ClockNeverDecreases, step, Clock(step));
    }

    private void Report(Invariant invariant, InvariantStep step, string? failure)
    {
        if (failure is not null)
        {
            _fail(invariant, $"{step.Describe()}: {failure}");
        }
    }

    // 4: final = initial + produced − consumed − spent + refunded, per item, over every item any of them names.
    private string? Conservation(InvariantStep step)
    {
        Dictionary<ItemId, long> spent = Quantities(step.Mutation?.Spent ?? []);
        Dictionary<ItemId, long> refunded = Quantities(step.Mutation?.Refunded ?? []);
        CountIf(spent.Count > 0, "steps that spent");
        CountIf(refunded.Count > 0, "steps that refunded");
        IEnumerable<ItemId> items = step.Before.Inventory.Keys.Concat(step.After.Inventory.Keys).Concat(step.Produced.Keys)
            .Concat(step.Consumed.Keys).Concat(spent.Keys).Concat(refunded.Keys).Distinct().Order();
        foreach (ItemId item in items)
        {
            long expected = step.Before.GetQuantity(item) + step.Produced.GetValueOrDefault(item) - step.Consumed.GetValueOrDefault(item)
                - spent.GetValueOrDefault(item) + refunded.GetValueOrDefault(item);
            if (step.After.GetQuantity(item) != expected)
            {
                return $"{item}: {step.Before.GetQuantity(item)} + produced {step.Produced.GetValueOrDefault(item)} − consumed "
                    + $"{step.Consumed.GetValueOrDefault(item)} − spent {spent.GetValueOrDefault(item)} + refunded "
                    + $"{refunded.GetValueOrDefault(item)} = {expected}, but the stock is {step.After.GetQuantity(item)}";
            }
        }

        return null;
    }

    // 5: no item below zero.
    private static string? NoNegativeStock(CityState city)
    {
        foreach (KeyValuePair<ItemId, long> stock in city.Inventory.Where(stock => stock.Value < 0))
        {
            return $"{stock.Key} is {stock.Value}";
        }

        return null;
    }

    // 7: the clock moves by exactly the requested seconds (0 for a command or a negative request), and the report says the same.
    private string? Clock(InvariantStep step)
    {
        long expected = Math.Max(step.Requested, 0);
        CountIf(step.Requested < 0, "steps asked to go back in time");
        return step.After.Clock == step.Before.Clock + expected && step.ReportedElapsed == expected
            ? null
            : $"asked for {step.Requested} s: the clock moved {step.After.Clock - step.Before.Clock} s, the report says {step.ReportedElapsed} s";
    }

    private void CountIf(bool happened, string what)
    {
        if (happened)
        {
            _count(what);
        }
    }

    private static Dictionary<ItemId, long> Quantities(IEnumerable<ItemQuantity> lines) =>
        lines.GroupBy(line => line.Item).ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
}
