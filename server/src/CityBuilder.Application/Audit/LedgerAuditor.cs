using CityBuilder.Application.Content;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Audit;

/// <summary>
/// The ledger audit (M0-64; <c>core-instructions/02-principles.md</c> rule 11, <c>06-data-model.md</c> → Ledger): for each stored city, the
/// ledger starts with the <c>STARTING_GRANT</c>, each row's <c>balance_after</c> runs on from the row before, and the sum of each item's
/// deltas equals the item's city total (<see cref="CityQueries.Totals"/>). It reads through <see cref="ILedgerAuditSource"/> and changes
/// nothing. Run by the integration tests (<c>docs/testing/README.md</c>); there is no endpoint. One city at a time today; it becomes
/// cross-player with the market in M2 (<c>26</c>).
/// </summary>
/// <param name="source">The stored cities.</param>
/// <param name="content">The game content the city totals are counted with.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class LedgerAuditor(ILedgerAuditSource source, IContentProvider content)
{
    /// <summary>The stored cities.</summary>
    private readonly ILedgerAuditSource _source = source ?? throw new ArgumentNullException(nameof(source));
    /// <summary>The game content.</summary>
    private readonly IContentProvider _content = content ?? throw new ArgumentNullException(nameof(content));

    /// <summary>Audits every stored city.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The number of cities and every mismatch.</returns>
    public async Task<LedgerAuditReport> AuditAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> cityIds = await _source.ListCityIdsAsync(cancellationToken);
        var mismatches = new List<LedgerMismatch>();
        foreach (Guid cityId in cityIds)
        {
            AuditedCity city = await _source.LoadAsync(cityId, cancellationToken);
            mismatches.AddRange(Audit(city, _content.Content));
        }

        return new LedgerAuditReport(cityIds.Count, mismatches);
    }

    /// <summary>Audits one stored city.</summary>
    /// <param name="cityId">The city.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>One city and its mismatches.</returns>
    /// <exception cref="Persistence.CityNotFoundException">No city has the id.</exception>
    public async Task<LedgerAuditReport> AuditCityAsync(Guid cityId, CancellationToken cancellationToken)
    {
        AuditedCity city = await _source.LoadAsync(cityId, cancellationToken);
        return new LedgerAuditReport(1, Audit(city, _content.Content));
    }

    /// <summary>
    /// The mismatches of one city: a missing starting grant first, then each broken balance chain in row order, then each item whose sum of
    /// deltas is not its city total, in item order (ordinal ids). An item in the ledger but not in the content has a city total of 0.
    /// </summary>
    /// <param name="audited">The city and its ledger rows in id order.</param>
    /// <param name="content">The game content.</param>
    /// <returns>The mismatches; empty when the ledger reconciles.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public static IReadOnlyList<LedgerMismatch> Audit(AuditedCity audited, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(audited);
        ArgumentNullException.ThrowIfNull(content);

        Guid cityId = audited.City.CityId;
        IReadOnlyList<AuditedLedgerRow> ledger = audited.Ledger;
        var mismatches = new List<LedgerMismatch>();
        if (ledger.Count == 0 || ledger[0].Reason != LedgerReason.StartingGrant)
        {
            mismatches.Add(new LedgerMismatch(cityId, LedgerMismatchKind.MissingStartingGrant, null, ledger.Count == 0 ? null : ledger[0].Id, 0, 0));
        }

        // Sorted, so the totals check below runs in item order; the chain check runs in row order.
        var sums = new SortedDictionary<ItemId, long>();
        foreach (AuditedLedgerRow row in ledger)
        {
            long sum = checked(sums.GetValueOrDefault(row.Item) + row.Delta);
            if (row.BalanceAfter != sum)
            {
                mismatches.Add(new LedgerMismatch(cityId, LedgerMismatchKind.BalanceChainBroken, row.Item, row.Id, sum, row.BalanceAfter));
            }

            sums[row.Item] = sum;
        }

        var totals = new Dictionary<ItemId, long>();
        foreach (ItemQuantity total in CityQueries.Totals(audited.City.State, content))
        {
            totals[total.Item] = total.Quantity;
            sums.TryAdd(total.Item, 0);
        }

        foreach (KeyValuePair<ItemId, long> sum in sums)
        {
            long actual = totals.GetValueOrDefault(sum.Key);
            if (sum.Value != actual)
            {
                mismatches.Add(new LedgerMismatch(cityId, LedgerMismatchKind.TotalMismatch, sum.Key, null, sum.Value, actual));
            }
        }

        return mismatches;
    }
}
