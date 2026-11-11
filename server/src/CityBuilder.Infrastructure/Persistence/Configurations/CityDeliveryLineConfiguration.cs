using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityDeliveryLineRow"/> to <c>city_delivery_lines</c> as core-instructions/06-data-model.md describes it (CR-0008 amendment 1).</summary>
public sealed class CityDeliveryLineConfiguration : IEntityTypeConfiguration<CityDeliveryLineRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityDeliveryLineRow> builder)
    {
        builder.ToTable("city_delivery_lines", table =>
        {
            table.HasCheckConstraint("ck_city_delivery_lines_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_city_delivery_lines_buildings", "source_id <> target_id");
            table.HasCheckConstraint("ck_city_delivery_lines_slot", "slot >= 1");
        });

        // The courier's columns lead the key, so the foreign key to the courier needs no index of its own (06: the ledger's is the only one).
        builder.HasKey(l => new { l.CityId, l.StationId, l.Slot, l.LineId }).HasName("pk_city_delivery_lines");
        builder.Property(l => l.CityId).HasColumnName("city_id");
        builder.Property(l => l.LineId).HasColumnName("line_id").ValueGeneratedNever();
        builder.Property(l => l.StationId).HasColumnName("station_id");
        builder.Property(l => l.Slot).HasColumnName("slot");
        builder.Property(l => l.SourceId).HasColumnName("source_id");
        builder.Property(l => l.TargetId).HasColumnName("target_id");
        builder.Property(l => l.ItemId).HasColumnName("item_id");
        builder.Property(l => l.Quantity).HasColumnName("quantity");

        // The courier owns its lines: the rows go with it.
        builder.HasOne<CityCourierRow>().WithMany().HasForeignKey(l => new { l.CityId, l.StationId, l.Slot })
            .HasConstraintName("fk_city_delivery_lines_city_couriers").OnDelete(DeleteBehavior.Cascade);
    }
}
