using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="LedgerEntryRow"/> to <c>ledger_entries</c> as core-instructions/06-data-model.md describes it.</summary>
public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntryRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LedgerEntryRow> builder)
    {
        builder.ToTable("ledger_entries");

        builder.HasKey(e => e.Id).HasName("pk_ledger_entries");
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(e => e.CityId).HasColumnName("city_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.Delta).HasColumnName("delta");
        builder.Property(e => e.BalanceAfter).HasColumnName("balance_after");
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.RefType).HasColumnName("ref_type");
        builder.Property(e => e.RefId).HasColumnName("ref_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => new { e.CityId, e.CreatedAt }).HasDatabaseName("ix_ledger_entries_city_id_created_at");
        // The ledger is the audit trail: a city with entries can't be deleted.
        builder.HasOne<CityRow>().WithMany().HasForeignKey(e => e.CityId)
            .HasConstraintName("fk_ledger_entries_cities_city_id").OnDelete(DeleteBehavior.Restrict);
    }
}
