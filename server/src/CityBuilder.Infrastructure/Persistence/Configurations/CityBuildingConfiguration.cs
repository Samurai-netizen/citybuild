using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityBuildingRow"/> to <c>city_buildings</c> as core-instructions/06-data-model.md describes it.</summary>
public sealed class CityBuildingConfiguration : IEntityTypeConfiguration<CityBuildingRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityBuildingRow> builder)
    {
        builder.ToTable("city_buildings", table =>
        {
            table.HasCheckConstraint("ck_city_buildings_x", "x >= 0");
            table.HasCheckConstraint("ck_city_buildings_y", "y >= 0");
            table.HasCheckConstraint("ck_city_buildings_level", "level >= 1");
            table.HasCheckConstraint("ck_city_buildings_assigned_workers", "assigned_workers >= 0");
            table.HasCheckConstraint("ck_city_buildings_labor_remaining", "labor_remaining >= 0");
        });

        builder.HasKey(b => new { b.CityId, b.BuildingId }).HasName("pk_city_buildings");
        builder.Property(b => b.CityId).HasColumnName("city_id");
        builder.Property(b => b.BuildingId).HasColumnName("building_id").ValueGeneratedNever();
        builder.Property(b => b.TypeId).HasColumnName("type_id");
        builder.Property(b => b.X).HasColumnName("x");
        builder.Property(b => b.Y).HasColumnName("y");
        builder.Property(b => b.Level).HasColumnName("level");
        builder.Property(b => b.AssignedWorkers).HasColumnName("assigned_workers");
        builder.Property(b => b.Phase).HasColumnName("phase");
        builder.Property(b => b.PhaseStartedAt).HasColumnName("phase_started_at");
        builder.Property(b => b.PhaseEndsAt).HasColumnName("phase_ends_at");
        builder.Property(b => b.TargetLevel).HasColumnName("target_level");
        builder.Property(b => b.CycleLabor).HasColumnName("cycle_labor");
        builder.Property(b => b.LaborRemaining).HasColumnName("labor_remaining");
        builder.Property(b => b.SegmentStartedAt).HasColumnName("segment_started_at");

        // No two buildings of a city on one cell.
        builder.HasIndex(b => new { b.CityId, b.X, b.Y }).IsUnique().HasDatabaseName("ux_city_buildings_city_id_x_y");
        // The city owns its buildings: the rows go with it.
        builder.HasOne<CityRow>().WithMany().HasForeignKey(b => b.CityId)
            .HasConstraintName("fk_city_buildings_cities_city_id").OnDelete(DeleteBehavior.Cascade);
    }
}
