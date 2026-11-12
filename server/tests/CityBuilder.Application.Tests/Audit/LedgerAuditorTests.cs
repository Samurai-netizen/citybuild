using CityBuilder.Application.Audit;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Persistence;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;

namespace CityBuilder.Application.Tests.Audit;

/// <summary>
/// <see cref="LedgerAuditor"/> on hand-made cities and ledgers (M0-64): a ledger that reconciles has no mismatch, and each kind of
/// mismatch is found with its item, row and both numbers. The city is <see cref="SettleFixture.SawmillRepository"/>'s: 70 coins in the
/// purse, 1 + 482 logs in two stores, no planks. The PostgreSQL run over HTTP is <c>LedgerAuditDatabaseTests</c>.
/// </summary>
public sealed class LedgerAuditorTests
{
    /// <summary>The currency.</summary>
    private static readonly ItemId _coins = new("coins");

    /// <summary>The sawmill city, its ledger checked against.</summary>
    private static readonly CityAggregate _city = SawmillCity(SettleFixture.CityId);

    /// <summary>
    /// A ledger that reconciles with the sawmill city: the grant of 100 coins, a 30-coin cost, 485 logs produced and 2 consumed.
    /// </summary>
    private static readonly AuditedLedgerRow[] _reconciling =
    [
        new(1, _coins, 100, 100, LedgerReason.StartingGrant),
        new(2, _coins, -30, 70, LedgerReason.CostPlace),
        new(3, SettleFixture.Logs, 485, 485, LedgerReason.SettlementProduced),
        new(4, SettleFixture.Logs, -2, 483, LedgerReason.SettlementConsumed),
    ];

    /// <summary>A ledger whose sums equal the city's totals and whose balances run on has no mismatch.</summary>
    [Fact]
    public void Audit_LedgerThatReconciles_FindsNothing()
    {
        Assert.Empty(LedgerAuditor.Audit(new AuditedCity(_city, _reconciling), SettleFixture.Content));
    }

    /// <summary>A city without ledger rows misses its grant, and each item with a total is a mismatch against a ledger sum of 0.</summary>
    [Fact]
    public void Audit_NoLedgerRows_ReportsMissingGrantAndEveryTotal()
    {
        IReadOnlyList<LedgerMismatch> mismatches = LedgerAuditor.Audit(new AuditedCity(_city, []), SettleFixture.Content);

        Assert.Equal(
            [
                new LedgerMismatch(SettleFixture.CityId, LedgerMismatchKind.MissingStartingGrant, null, null, 0, 0),
                new LedgerMismatch(SettleFixture.CityId, LedgerMismatchKind.TotalMismatch, _coins, null, 0, 70),
                new LedgerMismatch(SettleFixture.CityId, LedgerMismatchKind.TotalMismatch, SettleFixture.Logs, null, 0, 483),
            ],
            mismatches);
    }

    /// <summary>A ledger whose first row is not the grant (here a server panel adjustment, a reason the server never writes) is reported with that row.</summary>
    [Fact]
    public void Audit_FirstRowNotStartingGrant_ReportsMissingGrant()
    {
        AuditedLedgerRow[] ledger = [_reconciling[0] with { Reason = null }, .. _reconciling[1..]];

        LedgerMismatch mismatch = Assert.Single(LedgerAuditor.Audit(new AuditedCity(_city, ledger), SettleFixture.Content));

        Assert.Equal(new LedgerMismatch(SettleFixture.CityId, LedgerMismatchKind.MissingStartingGrant, null, 1, 0, 0), mismatch);
    }

    /// <summary>A row whose <c>balance_after</c> is not the balance before plus its delta is reported with the balance the chain runs to.</summary>
    [Fact]
    public void Audit_BalanceAfterOff_ReportsBrokenChain()
    {
        AuditedLedgerRow[] ledger = [.. _reconciling[..3], _reconciling[3] with { BalanceAfter = 480 }];

        LedgerMismatch mismatch = Assert.Single(LedgerAuditor.Audit(new AuditedCity(_city, ledger), SettleFixture.Content));

        Assert.Equal(new LedgerMismatch(SettleFixture.CityId, LedgerMismatchKind.BalanceChainBroken, SettleFixture.Logs, 4, 483, 480), mismatch);
    }

    /// <summary>A missing row (here the consumed logs) leaves the chain intact but the sum off the city total: reported with both numbers.</summary>
    [Fact]
    public void Audit_SumDiffersFromTotal_ReportsTotalMismatch()
    {
        LedgerMismatch mismatch = Assert.Single(LedgerAuditor.Audit(new AuditedCity(_city, _reconciling[..3]), SettleFixture.Content));

        Assert.Equal(new LedgerMismatch(SettleFixture.CityId, LedgerMismatchKind.TotalMismatch, SettleFixture.Logs, null, 485, 483), mismatch);
    }

    /// <summary>Rows of an item the content doesn't have count against a city total of 0.</summary>
    [Fact]
    public void Audit_ItemNotInContent_ReportsTotalMismatch()
    {
        var stone = new ItemId("stone");
        AuditedLedgerRow[] ledger = [.. _reconciling, new(5, stone, 5, 5, LedgerReason.SettlementProduced)];

        LedgerMismatch mismatch = Assert.Single(LedgerAuditor.Audit(new AuditedCity(_city, ledger), SettleFixture.Content));

        Assert.Equal(new LedgerMismatch(SettleFixture.CityId, LedgerMismatchKind.TotalMismatch, stone, null, 5, 0), mismatch);
    }

    /// <summary>A full run audits every city the source lists, in its order, and gathers the mismatches of each.</summary>
    /// <returns>A task that completes when the run is checked.</returns>
    [Fact]
    public async Task AuditAllAsync_TwoCities_CountsBothAndReportsTheBrokenOne()
    {
        var brokenId = new Guid("6f1d2c3b-0000-4000-8000-0000000000c2");
        var source = new InMemoryAuditSource(
            new AuditedCity(_city, _reconciling),
            new AuditedCity(SawmillCity(brokenId), _reconciling[..3]));
        var auditor = new LedgerAuditor(source, new SettleFixture.FixedContent(SettleFixture.Content));

        LedgerAuditReport report = await auditor.AuditAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, report.CitiesAudited);
        Assert.False(report.IsClean);
        Assert.Equal(brokenId, Assert.Single(report.Mismatches).CityId);
    }

    /// <summary>A one-city run audits that city only.</summary>
    /// <returns>A task that completes when the run is checked.</returns>
    [Fact]
    public async Task AuditCityAsync_ReconcilingCity_IsClean()
    {
        var auditor = new LedgerAuditor(new InMemoryAuditSource(new AuditedCity(_city, _reconciling)), new SettleFixture.FixedContent(SettleFixture.Content));

        LedgerAuditReport report = await auditor.AuditCityAsync(SettleFixture.CityId, TestContext.Current.CancellationToken);

        Assert.Equal(1, report.CitiesAudited);
        Assert.True(report.IsClean);
    }

    /// <summary>The sawmill city of <see cref="SettleFixture.SawmillRepository"/> under another id.</summary>
    /// <param name="cityId">The city's id.</param>
    /// <returns>The city.</returns>
    private static CityAggregate SawmillCity(Guid cityId) => new(
        cityId, SettleFixture.PlayerId, SettleFixture.SawmillState(), [], SettleFixture.LastSettled.ToUnixTimeSeconds(), 0, SettleFixture.Content.ContentVersion);

    /// <summary>An <see cref="ILedgerAuditSource"/> over cities held in memory, in the order given.</summary>
    /// <param name="cities">The cities and their ledgers.</param>
    private sealed class InMemoryAuditSource(params AuditedCity[] cities) : ILedgerAuditSource
    {
        /// <inheritdoc />
        public Task<IReadOnlyList<Guid>> ListCityIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([.. cities.Select(city => city.City.CityId)]);

        /// <inheritdoc />
        public Task<AuditedCity> LoadAsync(Guid cityId, CancellationToken cancellationToken) =>
            Task.FromResult(cities.SingleOrDefault(city => city.City.CityId == cityId) ?? throw new CityNotFoundException($"No city {cityId}."));
    }
}
