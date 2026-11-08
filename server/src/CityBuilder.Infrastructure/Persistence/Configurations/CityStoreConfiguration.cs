using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityStoreRow"/> to <c>city_stores</c> as core-instructions/06-data-model.md describes it (CR-0008).</summary>
public sealed class CityStoreConfiguration : IEntityTypeConfiguration<CityStoreRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityStoreRow> builder)
    {
        builder.ToTable("city_stores", table => table.HasCheckConstraint("ck_city_stores_quantity", "quantity >= 0"));

        builder.HasKey(s => new { s.CityId, s.BuildingId, s.ItemId }).HasName("pk_city_stores");
        builder.Property(s => s.CityId).HasColumnName("city_id");
        builder.Property(s => s.BuildingId).HasColumnName("building_id").ValueGeneratedNever();
        builder.Property(s => s.ItemId).HasColumnName("item_id");
        builder.Property(s => s.Quantity).HasColumnName("quantity");

        // The building owns its store: the rows go with it.
        builder.HasOne<CityBuildingRow>().WithMany().HasForeignKey(s => new { s.CityId, s.BuildingId })
            .HasConstraintName("fk_city_stores_city_buildings_building_id").OnDelete(DeleteBehavior.Cascade);
    }
}
