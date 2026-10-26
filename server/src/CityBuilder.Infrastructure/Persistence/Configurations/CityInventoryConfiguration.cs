using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityInventoryRow"/> to <c>city_inventory</c> as core-instructions/06-data-model.md describes it.</summary>
public sealed class CityInventoryConfiguration : IEntityTypeConfiguration<CityInventoryRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityInventoryRow> builder)
    {
        builder.ToTable("city_inventory", table => table.HasCheckConstraint("ck_city_inventory_quantity", "quantity >= 0"));

        builder.HasKey(i => new { i.CityId, i.ItemId }).HasName("pk_city_inventory");
        builder.Property(i => i.CityId).HasColumnName("city_id");
        builder.Property(i => i.ItemId).HasColumnName("item_id");
        builder.Property(i => i.Quantity).HasColumnName("quantity");

        // The city owns its inventory: the rows go with it.
        builder.HasOne<CityRow>().WithMany().HasForeignKey(i => i.CityId)
            .HasConstraintName("fk_city_inventory_cities_city_id").OnDelete(DeleteBehavior.Cascade);
    }
}
