using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityProjectNeedRow"/> to <c>city_project_needs</c> as core-instructions/06-data-model.md describes it (CR-0008).</summary>
public sealed class CityProjectNeedConfiguration : IEntityTypeConfiguration<CityProjectNeedRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityProjectNeedRow> builder)
    {
        builder.ToTable("city_project_needs", table => table.HasCheckConstraint("ck_city_project_needs_quantity", "quantity > 0"));

        builder.HasKey(n => new { n.CityId, n.BuildingId, n.ItemId }).HasName("pk_city_project_needs");
        builder.Property(n => n.CityId).HasColumnName("city_id");
        builder.Property(n => n.BuildingId).HasColumnName("building_id").ValueGeneratedNever();
        builder.Property(n => n.ItemId).HasColumnName("item_id");
        builder.Property(n => n.Quantity).HasColumnName("quantity");

        // The building owns its project: the rows go with it.
        builder.HasOne<CityBuildingRow>().WithMany().HasForeignKey(n => new { n.CityId, n.BuildingId })
            .HasConstraintName("fk_city_project_needs_city_buildings_building_id").OnDelete(DeleteBehavior.Cascade);
    }
}
