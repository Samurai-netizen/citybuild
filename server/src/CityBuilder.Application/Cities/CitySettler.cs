using System.Globalization;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Cities;

/// <summary>
/// The steps of one settlement of a locked city, shared by <see cref="SettleCity"/> and the command pipeline
/// (<c>core-instructions/03-architecture.md</c>, request lifecycle steps 5 to 8; <c>04-simulation-model.md</c>, sections 1, 2 and 7):
/// <see cref="Begin"/> advances the city by the server seconds since it was last settled, a command may then <see cref="RecordCommand"/> its
/// applied mutation, and <see cref="Close"/> runs the closing zero-length advance. After that it tells whether anything changed and gives the
/// ledger entries, in the order things happened. One instance per settlement, used in that order.
/// </summary>
internal sealed class CitySettler
{
    /// <summary>The <c>ref_type</c> of a command's ledger rows; their <c>ref_id</c> is the command's request id (<c>06</c>).</summary>
    public const string CommandLedgerRefType = "command";

    /// <summary>The city, changed in place.</summary>
    private readonly CityAggregate _city;
    /// <summary>The content the settlement runs with.</summary>
    private readonly GameContent _content;
    /// <summary>The server time of the settlement; the settlement's id is made from it.</summary>
    private readonly DateTimeOffset _now;
    /// <summary>The last settled second before the settlement.</summary>
    private readonly long _settledBefore;
    /// <summary>The content version before the settlement.</summary>
    private readonly string _contentBefore;
    /// <summary>The city's total of each item before the settlement: stores, loads and, for the currency, the purse (<c>04</c> section 16).</summary>
    private readonly Dictionary<ItemId, long> _quantitiesBefore;
    /// <summary>Everything an advance can change in the city's state, before the settlement (<see cref="StateFingerprint"/>).</summary>
    private readonly string _stateBefore;
    /// <summary>The server seconds the settlement covers.</summary>
    private readonly SettlementWindow _window;
    /// <summary>The report of the advance by the window's elapsed seconds.</summary>
    private readonly SettlementReport _advance;
    /// <summary>
    /// The recorded command: its ledger lines (refunds, then costs) and the <c>ref_id</c> of their rows; <see langword="null"/> when no command
    /// was recorded.
    /// </summary>
    private (IReadOnlyList<(ItemId Item, long Delta, LedgerReason Reason)> Lines, string RefId)? _command;
    /// <summary>The settlement, once <see cref="Close"/> has run.</summary>
    private Settlement? _settlement;

    /// <summary>Records the city before the settlement and advances it; see <see cref="Begin"/>.</summary>
    /// <param name="city">The locked city.</param>
    /// <param name="content">The game content.</param>
    /// <param name="now">The server time, read under the lock.</param>
    private CitySettler(CityAggregate city, GameContent content, DateTimeOffset now)
    {
        _city = city;
        _content = content;
        _now = now;
        _settledBefore = city.LastSettledUnixSec;
        _contentBefore = city.ContentVersion;
        _quantitiesBefore = Totals(city.State, content);
        _stateBefore = StateFingerprint(city.State);

        // ToUnixTimeSeconds rounds down (it divides ticks since year 1, which are never negative): 29.7 s is second 29. Each window starts at
        // the second the last one ended, so the dropped fractions are never lost, only counted by a later settlement.
        _window = SettlementWindow.Compute(_settledBefore, now.ToUnixTimeSeconds());
        _advance = city.Advance(content, _window.ElapsedSeconds);
        city.MarkSettled(_window.NewLastSettledUnixSec, content.ContentVersion);
    }

    /// <summary>
    /// Whether the settlement changed the city: the clock or the content version moved, the report is not empty, the state changed in a way the
    /// report doesn't show (a cycle without inputs starting, a trip bringing its whole load back, a research timer starting), or a command was
    /// recorded. Known once <see cref="Close"/> has run.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Close"/> hasn't run.</exception>
    public bool Changed
    {
        get
        {
            Settlement settlement = ClosedSettlement();
            return _command is not null || _city.LastSettledUnixSec != _settledBefore || _city.ContentVersion != _contentBefore
                || !settlement.IsEmpty || _stateBefore != StateFingerprint(_city.State);
        }
    }

    /// <summary>
    /// Begins the settlement of the locked city: records it as it is, advances it by the server seconds from its last settled second to
    /// <paramref name="now"/> (every timer, cycle start and walk of the window, with no cap), and
    /// records the new last settled second and content version.
    /// </summary>
    /// <param name="city">The acting player's city, locked in its transaction.</param>
    /// <param name="content">The game content.</param>
    /// <param name="now">The server time, read under the lock: a request that waited for the lock settles from where the one before it ended.</param>
    /// <returns>The settlement, advanced; record a command or close it next.</returns>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    public static CitySettler Begin(CityAggregate city, GameContent content, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(content);
        return new CitySettler(city, content, now);
    }

    /// <summary>
    /// Records a command's applied mutation, between the advance and the closing advance (<c>03</c>, request lifecycle step 6): its ledger
    /// lines are the items it gave back (<paramref name="refundReason"/>), then the items it paid (<paramref name="costReason"/>), each per
    /// item in ordinal order, as the engine applies them (refund first, <c>04</c> section 8). Once, before <see cref="Close"/>.
    /// </summary>
    /// <param name="applied">The engine's result of the mutation, applied.</param>
    /// <param name="costReason">The ledger reason of what it paid, or <see langword="null"/> for a command that never pays.</param>
    /// <param name="refundReason">The ledger reason of what it gave back, or <see langword="null"/> for a command that never refunds.</param>
    /// <param name="requestId">The command's request id: the <c>ref_id</c> of its rows.</param>
    /// <exception cref="InvalidOperationException">The mutation was rejected, a command was already recorded, the settlement is closed, or the
    /// mutation paid or refunded without a reason for it (a programmer error in the command).</exception>
    public void RecordCommand(MutationResult applied, LedgerReason? costReason, LedgerReason? refundReason, Guid requestId)
    {
        ArgumentNullException.ThrowIfNull(applied);
        if (!applied.IsOk || _command is not null || _settlement is not null)
        {
            throw new InvalidOperationException(
                "A settlement records one applied command, between its advance and its close; a rejection records nothing.");
        }

        var lines = new List<(ItemId Item, long Delta, LedgerReason Reason)>();
        AddLines(lines, applied.Refunded, refundReason, sign: 1);
        AddLines(lines, applied.Spent, costReason, sign: -1);
        _command = (lines, requestId.ToString());
    }

    /// <summary>
    /// Ends the settlement with the closing zero-length advance (<c>04</c> section 7): after a window longer than the cap it restarts
    /// production at once, and after a command it starts what the mutation made possible. Once.
    /// </summary>
    /// <returns>The settlement: the advance and the closing advance as one.</returns>
    /// <exception cref="InvalidOperationException">The settlement is closed already.</exception>
    public Settlement Close()
    {
        if (_settlement is not null)
        {
            throw new InvalidOperationException("A settlement is closed once.");
        }

        SettlementReport closing = _city.Advance(_content, 0);
        long clockBehindSeconds = _window.IsClockBehind ? checked(_settledBefore - _now.ToUnixTimeSeconds()) : 0;
        _settlement = new Settlement(Guid.CreateVersion7(_now), _window, clockBehindSeconds, _advance, closing);
        return _settlement;
    }

    /// <summary>
    /// The ledger entries of the settlement and the command, in the order things happened, each balance running on from the stock before
    /// (<c>06</c>, <c>ledger_entries</c>). Without a command: per item and direction one row, the settlement's (<see cref="Settlement.LedgerEntries"/>).
    /// With one: the advance's rows, the command's (<see cref="CommandLedgerRefType"/>), then the closing advance's; the closing advance can
    /// consume what an upgrade refunded, so its rows can't come first. First checks that the rows account for the city's stock.
    /// </summary>
    /// <returns>The entries, in order; empty when no quantity changed.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Close"/> hasn't run, or the changes don't account for the city's stock (an
    /// engine bug, which must not be stored).</exception>
    public IReadOnlyList<LedgerEntry> LedgerEntries()
    {
        Settlement settlement = ClosedSettlement();
        CheckStockAddsUp(settlement);
        if (_command is not { } command)
        {
            return settlement.LedgerEntries(_quantitiesBefore);
        }

        string settlementRef = settlement.Id.ToString();
        var writer = new LedgerWriter(_quantitiesBefore);
        writer.AddSettlement(SettlementTotals.Of(settlement.Advance), Settlement.LedgerRefType, settlementRef);
        foreach ((ItemId item, long delta, LedgerReason reason) in command.Lines)
        {
            writer.Add(item, delta, reason, CommandLedgerRefType, command.RefId);
        }

        writer.AddSettlement(SettlementTotals.Of(settlement.Closing), Settlement.LedgerRefType, settlementRef);
        return writer.Entries;
    }

    /// <summary>
    /// Adds a mutation's item list as ledger lines, aggregated per item in ordinal order; lines of 0 are left out.
    /// </summary>
    /// <param name="lines">The command's lines so far.</param>
    /// <param name="items">The items the mutation paid or gave back.</param>
    /// <param name="reason">Their ledger reason, or <see langword="null"/> when the command has none.</param>
    /// <param name="sign">-1 for what was paid, 1 for what was given back.</param>
    /// <exception cref="InvalidOperationException">Items moved but the command has no reason for them.</exception>
    private static void AddLines(
        List<(ItemId Item, long Delta, LedgerReason Reason)> lines, IReadOnlyList<ItemQuantity> items, LedgerReason? reason, int sign)
    {
        IEnumerable<(ItemId Item, long Quantity)> moved = items
            .GroupBy(line => line.Item)
            .Select(group => (group.Key, group.Aggregate(0L, (sum, line) => checked(sum + line.Quantity))))
            .Where(line => line.Item2 != 0)
            .OrderBy(line => line.Key);
        foreach ((ItemId item, long quantity) in moved)
        {
            if (reason is null)
            {
                throw new InvalidOperationException($"The command moved {quantity} {item}, but it names no ledger reason for that.");
            }

            lines.Add((item, checked(sign * quantity), reason.Value));
        }
    }

    /// <summary>
    /// Checks that the settlement and the command account for every change of the city's totals (<c>04</c> section 10, conservation): for
    /// every item, the total before plus what was produced and refunded, minus what was consumed and delivered, plus the command's lines, is
    /// the total now (stores, loads, the purse). The ledger rows are built from the same numbers, so their sums then equal the totals' deltas
    /// and each item's last balance is its total.
    /// </summary>
    /// <param name="settlement">The closed settlement.</param>
    /// <exception cref="InvalidOperationException">An item's total doesn't follow from the settlement and the command.</exception>
    private void CheckStockAddsUp(Settlement settlement)
    {
        var expected = new Dictionary<ItemId, long>(_quantitiesBefore);
        foreach ((ItemId item, long delta) in settlement.Produced
            .Concat(settlement.Refunded)
            .Concat(settlement.Consumed.Select(line => KeyValuePair.Create(line.Key, -line.Value)))
            .Concat(settlement.Delivered.Select(line => KeyValuePair.Create(line.Key, -line.Value)))
            .Concat((_command?.Lines ?? []).Select(line => KeyValuePair.Create(line.Item, line.Delta))))
        {
            expected[item] = checked(expected.GetValueOrDefault(item) + delta);
        }

        Dictionary<ItemId, long> now = Totals(_city.State, _content);
        foreach (ItemId item in expected.Keys.Concat(now.Keys).Distinct().Order())
        {
            if (expected.GetValueOrDefault(item) != now.GetValueOrDefault(item))
            {
                throw new InvalidOperationException(
                    $"The settlement leaves {item} at {expected.GetValueOrDefault(item)}, but the city holds {now.GetValueOrDefault(item)}.");
            }
        }
    }

    /// <summary>The city's total of every item of the content (<see cref="CityQueries.Totals"/>), by item.</summary>
    /// <param name="state">The city.</param>
    /// <param name="content">The game content.</param>
    /// <returns>Each item's total, 0 included.</returns>
    private static Dictionary<ItemId, long> Totals(CityState state, GameContent content) =>
        CityQueries.Totals(state, content).ToDictionary(total => total.Item, total => total.Quantity);

    /// <summary>The settlement, once closed.</summary>
    /// <returns>The settlement.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Close"/> hasn't run.</exception>
    private Settlement ClosedSettlement() =>
        _settlement ?? throw new InvalidOperationException("The settlement isn't closed yet: run the closing advance first.");

    /// <summary>
    /// Everything an advance can change in a city as one text, to tell whether the settlement changed something its report doesn't show (a
    /// cycle without inputs starting, a trip whose whole load went back, a research timer started by a delivery): every building's phase
    /// fields, workers, store and needs, the purse, the research with its times and needs, the trips under way, and every courier's state, walk,
    /// stamina and loads (a walk started or ended without goods, stamina spent). The clock is left out (the window says whether it moved).
    /// </summary>
    /// <param name="city">The city.</param>
    /// <returns>The text; equal for two states an advance can't tell apart.</returns>
    private static string StateFingerprint(CityState city)
    {
        var text = new System.Text.StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"purse {city.Purse}\n");
        foreach (Building building in city.Buildings)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"b {building.Id} L{building.Level} w{building.AssignedWorkers} {building.Phase} {building.PhaseStartedAt}..{building.PhaseEndsAt} ");
            text.Append(CultureInfo.InvariantCulture, $"t{building.TargetLevel} {building.CycleLabor}/{building.LaborRemaining}@{building.SegmentStartedAt}");
            AppendAmounts(text, " s", building.Store);
            AppendAmounts(text, " n", building.Needs);
            text.Append('\n');
        }

        if (city.ActiveResearch is { } research)
        {
            text.Append(CultureInfo.InvariantCulture, $"r {research.Id} {research.StartedAt}..{research.EndsAt}");
            AppendAmounts(text, " n", research.Needs);
            text.Append('\n');
        }

        foreach (Trip trip in city.Trips)
        {
            text.Append(CultureInfo.InvariantCulture, $"trip {trip.Id}\n");
        }

        foreach (Courier courier in city.Couriers)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"c {courier.StationId}/{courier.Slot} {courier.State} {courier.ToStop} {courier.DepartedAt}..{courier.ArrivesAt} {courier.Stamina}");
            foreach (CourierLoad load in courier.Loads)
            {
                text.Append(CultureInfo.InvariantCulture, $" {load.Item}@{load.FromId}={load.Quantity}");
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Appends quantities as <c> prefix item=quantity</c> pairs in their (ordinal item) order.</summary>
    /// <param name="text">The text so far.</param>
    /// <param name="prefix">What marks the list.</param>
    /// <param name="amounts">The quantities.</param>
    private static void AppendAmounts(System.Text.StringBuilder text, string prefix, IReadOnlyDictionary<ItemId, long> amounts)
    {
        text.Append(prefix);
        foreach (KeyValuePair<ItemId, long> amount in amounts)
        {
            text.Append(CultureInfo.InvariantCulture, $" {amount.Key}={amount.Value}");
        }
    }
}
