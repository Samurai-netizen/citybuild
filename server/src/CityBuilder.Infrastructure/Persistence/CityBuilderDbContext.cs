using CityBuilder.Infrastructure.Persistence.Configurations;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;

namespace CityBuilder.Infrastructure.Persistence;

/// <summary>
/// The EF Core context over the game database (core-instructions/06-data-model.md): one set per table, each mapped by its configuration
/// in <c>Persistence/Configurations</c>. Only Infrastructure uses it; Application sees ports (M0-20).
/// </summary>
/// <param name="options">The options with the Npgsql provider and the connection string.</param>
public sealed class CityBuilderDbContext(DbContextOptions<CityBuilderDbContext> options) : DbContext(options)
{
    /// <summary>The <c>players</c> table.</summary>
    public DbSet<PlayerRow> Players => Set<PlayerRow>();

    /// <summary>The <c>cities</c> table.</summary>
    public DbSet<CityRow> Cities => Set<CityRow>();

    /// <summary>The <c>city_inventory</c> table.</summary>
    public DbSet<CityInventoryRow> CityInventory => Set<CityInventoryRow>();

    /// <summary>The <c>city_buildings</c> table.</summary>
    public DbSet<CityBuildingRow> CityBuildings => Set<CityBuildingRow>();

    /// <summary>The <c>city_research</c> table.</summary>
    public DbSet<CityResearchRow> CityResearch => Set<CityResearchRow>();

    /// <summary>The <c>command_receipts</c> table.</summary>
    public DbSet<CommandReceiptRow> CommandReceipts => Set<CommandReceiptRow>();

    /// <summary>The <c>ledger_entries</c> table.</summary>
    public DbSet<LedgerEntryRow> LedgerEntries => Set<LedgerEntryRow>();

    /// <summary>Applies the configuration of every table, one by one, so the list of tables is visible here.</summary>
    /// <param name="modelBuilder">The builder of the model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new PlayerConfiguration());
        modelBuilder.ApplyConfiguration(new CityConfiguration());
        modelBuilder.ApplyConfiguration(new CityInventoryConfiguration());
        modelBuilder.ApplyConfiguration(new CityBuildingConfiguration());
        modelBuilder.ApplyConfiguration(new CityResearchConfiguration());
        modelBuilder.ApplyConfiguration(new CommandReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new LedgerEntryConfiguration());
    }
}
