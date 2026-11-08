using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityCourierLoadRow"/> to <c>city_courier_loads</c> as core-instructions/06-data-model.md describes it (CR-0008).</summary>
public sealed class CityCourierLoadConfiguration : IEntityTypeConfiguration<CityCourierLoadRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityCourierLoadRow> builder)
    {
        builder.ToTable("city_courier_loads", table => table.HasCheckConstraint("ck_city_courier_loads_quantity", "quantity > 0"));

        builder.HasKey(l => new { l.CityId, l.StationId, l.Slot, l.ItemId, l.FromBuildingId }).HasName("pk_city_courier_loads");
        builder.Property(l => l.CityId).HasColumnName("city_id");
        builder.Property(l => l.StationId).HasColumnName("station_id").ValueGeneratedNever();
        builder.Property(l => l.Slot).HasColumnName("slot").ValueGeneratedNever();
        builder.Property(l => l.ItemId).HasColumnName("item_id");
        builder.Property(l => l.FromBuildingId).HasColumnName("from_building_id").ValueGeneratedNever();
        builder.Property(l => l.Quantity).HasColumnName("quantity");

        // The courier owns its load: the rows go with it.
        builder.HasOne<CityCourierRow>().WithMany().HasForeignKey(l => new { l.CityId, l.StationId, l.Slot })
            .HasConstraintName("fk_city_courier_loads_city_couriers_slot").OnDelete(DeleteBehavior.Cascade);
    }
}
