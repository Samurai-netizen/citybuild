using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Settlement;

namespace CityBuilder.Application.Cities;

/// <summary>
/// One settlement of a city (<c>core-instructions/04-simulation-model.md</c>, sections 7 and 11): the advance by the window's elapsed seconds
/// and the closing zero-length advance after it, as one. The closing advance restarts production after a window longer than the cap, so its
/// consumption belongs to the settlement. The totals are those of both advances; the ledger rows are aggregated over both, one per item and
/// direction, and carry the settlement's id. Immutable.
/// </summary>
public sealed class Settlement
{
    /// <summary>The <c>ref_type</c> of a settlement's ledger rows; their <c>ref_id</c> is <see cref="Id"/> (<c>06</c>).</summary>
    public const string LedgerRefType = "settlement";

    /// <summary>Combines the two advances of one settlement.</summary>
    /// <param name="id">The settlement's id, new for each settlement.</param>
    /// <param name="window">The server seconds the settlement covered.</param>
    /// <param name="advance">The report of the advance by the window's elapsed seconds.</param>
    /// <param name="closing">The report of the closing zero-length advance.</param>
    internal Settlement(Guid id, SettlementWindow window, SettlementReport advance, SettlementReport closing)
    {
        Id = id;
        Window = window;
        Advance = advance;
        Closing = closing;
        ElapsedSeconds = advance.ElapsedSeconds;
        ProductionSeconds = advance.ProductionSeconds;
        Produced = Sum(advance.Produced, closing.Produced);
        Consumed = Sum(advance.Consumed, closing.Consumed);
        CompletedBuildings = advance.CompletedBuildings.Concat(closing.CompletedBuildings).Distinct().Order().ToArray();
        CompletedResearch = advance.CompletedResearch.Concat(closing.CompletedResearch).Distinct().Order().ToArray();
        EraChanged = advance.EraChanged || closing.EraChanged;
    }

    /// <summary>The report of the advance by the window's elapsed seconds: what happened before a command's mutation.</summary>
    internal SettlementReport Advance { get; }

    /// <summary>The report of the closing zero-length advance: what happened after a command's mutation.</summary>
    internal SettlementReport Closing { get; }

    /// <summary>The settlement's id: the <c>ref_id</c> of its ledger rows.</summary>
    public Guid Id { get; }

    /// <summary>The server seconds the settlement covered; <see cref="SettlementWindow.IsClockBehind"/> calls for a warning (<c>04</c> section 1).</summary>
    public SettlementWindow Window { get; }

    /// <summary>The seconds the city clock moved (the closing advance moves it by none).</summary>
    public long ElapsedSeconds { get; }

    /// <summary>The seconds of the window in which cycles could start: <c>min(ElapsedSeconds, MaxOfflineProductionSeconds)</c>.</summary>
    public long ProductionSeconds { get; }

    /// <summary>The outputs of the cycles that completed, per item in ordinal order; only items above 0.</summary>
    public IReadOnlyDictionary<ItemId, long> Produced { get; }

    /// <summary>The inputs of the cycles that started, the restarts of the closing advance included, per item in ordinal order; only items above 0.</summary>
    public IReadOnlyDictionary<ItemId, long> Consumed { get; }

    /// <summary>The buildings whose construction or upgrade completed, by ascending id, each once.</summary>
    public IReadOnlyList<int> CompletedBuildings { get; }

    /// <summary>The research nodes that completed, in ordinal id order.</summary>
    public IReadOnlyList<ResearchId> CompletedResearch { get; }

    /// <summary>Whether the city entered a new era; false until the era transition exists (M2).</summary>
    public bool EraChanged { get; }

    /// <summary>Whether nothing happened: nothing produced, consumed or completed (the clock may still have moved).</summary>
    public bool IsEmpty =>
        Produced.Count == 0 && Consumed.Count == 0 && CompletedBuildings.Count == 0 && CompletedResearch.Count == 0 && !EraChanged;

    /// <summary>
    /// The settlement's ledger rows (<c>06</c>, <c>ledger_entries</c>): per item in ordinal order, <c>SETTLEMENT_PRODUCED</c> with the item's
    /// total output, then <c>SETTLEMENT_CONSUMED</c> with its total input as a negative delta. Each balance runs on from the quantity before
    /// the settlement, so the last one of an item is that quantity plus what was produced minus what was consumed. Produced comes first: an
    /// item can be consumed after it was produced in the same window, and a balance is never below 0 that way.
    /// </summary>
    /// <param name="quantitiesBefore">The city's quantity of each item before the settlement; an item that is missing had 0.</param>
    /// <returns>The rows, in order; empty when nothing was produced or consumed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="quantitiesBefore"/> is null.</exception>
    public IReadOnlyList<LedgerEntry> LedgerEntries(IReadOnlyDictionary<ItemId, long> quantitiesBefore)
    {
        ArgumentNullException.ThrowIfNull(quantitiesBefore);

        var writer = new LedgerWriter(quantitiesBefore);
        writer.AddProduction(Produced, Consumed, LedgerRefType, Id.ToString());
        return writer.Entries;
    }

    /// <summary>The per-item sum of two totals, in ordinal item order.</summary>
    /// <param name="first">The first totals.</param>
    /// <param name="second">The second totals.</param>
    /// <returns>Each item of either with the sum of its quantities.</returns>
    private static SortedDictionary<ItemId, long> Sum(IReadOnlyDictionary<ItemId, long> first, IReadOnlyDictionary<ItemId, long> second)
    {
        var sum = new SortedDictionary<ItemId, long>();
        foreach (KeyValuePair<ItemId, long> line in first.Concat(second))
        {
            sum[line.Key] = checked(sum.GetValueOrDefault(line.Key) + line.Value);
        }

        return sum;
    }
}
