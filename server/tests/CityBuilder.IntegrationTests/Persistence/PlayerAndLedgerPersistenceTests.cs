using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Persistence;
using CityBuilder.Application.Players;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Mapping;
using CityBuilder.Infrastructure.Persistence.Repositories;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The rest of the persistence step without a database: the ledger reasons' texts, a receipt's mapping and back, what
/// <see cref="PlayerRepository.Add"/> stages, and the registration of the ports in the server.
/// </summary>
public sealed class PlayerAndLedgerPersistenceTests
{
    /// <summary>Each ledger reason is stored as the text <c>06</c> lists for it.</summary>
    [Theory]
    [InlineData(LedgerReason.StartingGrant, "STARTING_GRANT")]
    [InlineData(LedgerReason.SettlementProduced, "SETTLEMENT_PRODUCED")]
    [InlineData(LedgerReason.SettlementConsumed, "SETTLEMENT_CONSUMED")]
    [InlineData(LedgerReason.CostPlace, "COST_PLACE")]
    [InlineData(LedgerReason.CostUpgrade, "COST_UPGRADE")]
    [InlineData(LedgerReason.RefundUpgrade, "REFUND_UPGRADE")]
    [InlineData(LedgerReason.CostResearch, "COST_RESEARCH")]
    public void ReasonText_EachReason_IsTheTextOfTheDataModel(LedgerReason reason, string text)
    {
        Assert.Equal(text, LedgerMapping.ReasonText(reason));
    }

    /// <summary>The theory above covers every reason: a new reason fails here until it has its text and a row there.</summary>
    [Fact]
    public void ReasonText_TheTestedReasons_AreAllTheReasons()
    {
        Assert.Equal(7, Enum.GetValues<LedgerReason>().Length);
    }

    /// <summary>A receipt mapped to its row and back is the same receipt: request id, command type, hash and response text.</summary>
    [Fact]
    public void ToReceipt_RowOfAReceipt_GivesTheReceiptBack()
    {
        byte[] hash = Enumerable.Range(0, CommandReceipt.PayloadHashLength).Select(value => (byte)value).ToArray();
        var receipt = new CommandReceipt(new Guid("0b7e9c4a-0000-4000-8000-0000000000b1"), "place-building", hash, "{\"b\": 1}");

        CommandReceipt back = LedgerMapping.ToReceipt(LedgerMapping.ToRow(Guid.NewGuid(), receipt, DateTimeOffset.UnixEpoch));

        Assert.Equal((receipt.RequestId, receipt.CommandType, receipt.ResponseJson), (back.RequestId, back.CommandType, back.ResponseJson));
        Assert.Equal(hash, back.PayloadHash.ToArray());
    }

    /// <summary>A new player is staged as an insert with its id, name and now as <c>created_at</c>.</summary>
    [Fact]
    public void Add_NewPlayer_StagesItsRowWithNow()
    {
        var now = new DateTimeOffset(2026, 10, 26, 18, 30, 0, TimeSpan.Zero);
        var id = new Guid("5a1c0f00-0000-4000-8000-000000000001");
        using CityBuilderDbContext db = PersistenceTestContext.Create();

        new PlayerRepository(db, new FixedClock(now)).Add(new Player(id, "dev1"));

        PlayerRow row = Assert.Single(db.ChangeTracker.Entries<PlayerRow>(), entry => entry.State == EntityState.Added).Entity;
        Assert.Equal((id, "dev1", now), (row.Id, row.DevName, row.CreatedAt));
    }

    /// <summary>The server resolves the ports to the Infrastructure classes, scoped: within one request scope they share one context.</summary>
    [Fact]
    public void AddCityBuilderPersistence_InTheServer_RegistersThePortsScoped()
    {
        using var factory = new ApiFactory();
        using IServiceScope scope = factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.IsType<CityRepository>(services.GetRequiredService<ICityRepository>());
        Assert.IsType<PlayerRepository>(services.GetRequiredService<IPlayerRepository>());
        Assert.IsType<UnitOfWork>(services.GetRequiredService<IUnitOfWork>());
        Assert.IsType<CommandReceiptRepository>(services.GetRequiredService<ICommandReceiptRepository>());
        Assert.Same(services.GetRequiredService<ICityRepository>(), services.GetRequiredService<ICityRepository>());
        Assert.Same(services.GetRequiredService<CityBuilderDbContext>(), services.GetRequiredService<CityBuilderDbContext>());
    }
}
