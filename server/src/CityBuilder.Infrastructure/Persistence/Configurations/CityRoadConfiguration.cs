using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityRoadRow"/> to <c>city_roads</c> as core-instructions/06-data-model.md describes it (CR-0008).</summary>
public sealed class CityRoadConfiguration : IEntityTypeConfiguration<CityRoadRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityRoadRow> builder)
    {
        builder.ToTable("city_roads", table =>
        {
            table.HasCheckConstraint("ck_city_roads_x", "x >= 0");
            table.HasCheckConstraint("ck_city_roads_y", "y >= 0");
            table.HasCheckConstraint("ck_city_roads_level", "level >= 1");
        });

        // One road per tile: the tile is the key.
        builder.HasKey(r => new { r.CityId, r.X, r.Y }).HasName("pk_city_roads");
        builder.Property(r => r.CityId).HasColumnName("city_id");
        builder.Property(r => r.X).HasColumnName("x").ValueGeneratedNever();
        builder.Property(r => r.Y).HasColumnName("y").ValueGeneratedNever();
        builder.Property(r => r.Level).HasColumnName("level");

        // The city owns its roads: the rows go with it.
        builder.HasOne<CityRow>().WithMany().HasForeignKey(r => r.CityId)
            .HasConstraintName("fk_city_roads_cities_city_id").OnDelete(DeleteBehavior.Cascade);
    }
}
