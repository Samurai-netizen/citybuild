using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Settlement;

namespace CityBuilder.Application.Cities;

/// <summary>
/// One settlement of a city (<c>core-instructions/04-simulation-model.md</c>, sections 7 and 11): the advance by the window's elapsed seconds
/// and the closing zero-length advance after it, as one. The closing advance starts the cycles a command's mutation made startable, so its
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
    /// <param name="clockBehindSeconds">How far the server second was behind the last settled second; 0 unless the clock is behind.</param>
    /// <param name="advance">The report of the advance by the window's elapsed seconds.</param>
    /// <param name="closing">The report of the closing zero-length advance.</param>
    internal Settlement(Guid id, SettlementWindow window, long clockBehindSeconds, SettlementReport advance, SettlementReport closing)
    {
        Id = id;
        Window = window;
        ClockBehindSeconds = clockBehindSeconds;
        Advance = advance;
        Closing = closing;
        ElapsedSeconds = advance.ElapsedSeconds;
        CourierSeconds = Math.Max(advance.CourierSeconds, closing.CourierSeconds);
        TiredCouriers = advance.TiredCouriers.Concat(closing.TiredCouriers)
            .OrderBy(tired => tired.StationId).ThenBy(tired => tired.Slot).ThenBy(tired => tired.At).ToArray();
        Produced = Sum(advance.Produced, closing.Produced);
        Consumed = Sum(advance.Consumed, closing.Consumed);
        Delivered = Sum(advance.Delivered, closing.Delivered);
        Moved = Sum(advance.Moved, closing.Moved);
        Refunded = Sum(advance.Refunded, closing.Refunded);
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

    /// <summary>
    /// How many seconds the server clock was behind the city's last settled second (<see cref="SettlementWindow.IsClockBehind"/>); 0 when it
    /// wasn't. The server logs it as a warning (<c>04</c> section 1); Application has no logger, so it carries the number out.
    /// </summary>
    public long ClockBehindSeconds { get; }

    /// <summary>The seconds the city clock moved (the closing advance moves it by none).</summary>
    public long ElapsedSeconds { get; }

    /// <summary>
    /// The most stamina one courier spent in the settlement, in seconds (<c>04</c> sections 2 and 11): the larger of the two advances' (the
    /// closing advance spends none, as no walk ends in it after the window); 0 while the player was present.
    /// </summary>
    public long CourierSeconds { get; }

    /// <summary>The couriers whose stamina ran out in the settlement, by (station, slot), each with the city second it did.</summary>
    public IReadOnlyList<TiredCourier> TiredCouriers { get; }

    /// <summary>The outputs of the cycles that completed, per item in ordinal order; only items above 0.</summary>
    public IReadOnlyDictionary<ItemId, long> Produced { get; }

    /// <summary>The inputs of the cycles that started, the restarts of the closing advance included, per item in ordinal order; only items above 0.</summary>
    public IReadOnlyDictionary<ItemId, long> Consumed { get; }

    /// <summary>The goods walkers handed to projects, used up there, per item in ordinal order; only items above 0 (CR-0008).</summary>
    public IReadOnlyDictionary<ItemId, long> Delivered { get; }

    /// <summary>The goods walkers put into stores, per item in ordinal order; only items above 0. They change no total, so no ledger row.</summary>
    public IReadOnlyDictionary<ItemId, long> Moved { get; }

    /// <summary>The inputs given back when an upgrade started at its last delivery, per item in ordinal order; only items above 0.</summary>
    public IReadOnlyDictionary<ItemId, long> Refunded { get; }

    /// <summary>The buildings whose construction or upgrade completed, by ascending id, each once.</summary>
    public IReadOnlyList<int> CompletedBuildings { get; }

    /// <summary>The research nodes that completed, in ordinal id order.</summary>
    public IReadOnlyList<ResearchId> CompletedResearch { get; }

    /// <summary>Whether the city entered a new era; false until the era transition exists (M2).</summary>
    public bool EraChanged { get; }

    /// <summary>
    /// Whether the report is empty: nothing produced, consumed, delivered, moved, refunded or completed, and no courier tired (the clock may
    /// still have moved, a trip may have brought all its load back, and a courier may have spent stamina without tiring).
    /// </summary>
    public bool IsEmpty =>
        Produced.Count == 0 && Consumed.Count == 0 && Delivered.Count == 0 && Moved.Count == 0 && Refunded.Count == 0
        && CompletedBuildings.Count == 0 && CompletedResearch.Count == 0 && !EraChanged && TiredCouriers.Count == 0;

    /// <summary>
    /// The settlement's ledger rows (<c>06</c>, <c>ledger_entries</c>; <see cref="LedgerWriter.AddSettlement"/>): per item in ordinal order,
    /// <c>SETTLEMENT_PRODUCED</c>, <c>SETTLEMENT_REFUNDED</c>, then <c>SETTLEMENT_CONSUMED</c> and <c>SETTLEMENT_DELIVERED</c> as negative
    /// deltas. Each balance runs on from the city total before the settlement, so the last one of an item is its total after.
    /// </summary>
    /// <param name="quantitiesBefore">The city's total of each item before the settlement; an item that is missing had 0.</param>
    /// <returns>The rows, in order; empty when no total changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="quantitiesBefore"/> is null.</exception>
    public IReadOnlyList<LedgerEntry> LedgerEntries(IReadOnlyDictionary<ItemId, long> quantitiesBefore)
    {
        ArgumentNullException.ThrowIfNull(quantitiesBefore);

        var writer = new LedgerWriter(quantitiesBefore);
        writer.AddSettlement(new SettlementTotals(Produced, Consumed, Delivered, Refunded), LedgerRefType, Id.ToString());
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
