using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityCourierRow"/> to <c>city_couriers</c> as core-instructions/06-data-model.md describes it (CR-0008).</summary>
public sealed class CityCourierConfiguration : IEntityTypeConfiguration<CityCourierRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityCourierRow> builder)
    {
        builder.ToTable("city_couriers", table =>
        {
            table.HasCheckConstraint("ck_city_couriers_slot", "slot >= 1");
            table.HasCheckConstraint("ck_city_couriers_to_stop", "to_stop >= 0");
            table.HasCheckConstraint("ck_city_couriers_stamina_seconds", "stamina_seconds >= 0");
            table.HasCheckConstraint(
                "ck_city_couriers_walk", "(departed_at is null and arrives_at is null) or (departed_at is not null and arrives_at is not null and departed_at >= 0 and arrives_at > departed_at)");
        });

        builder.HasKey(c => new { c.CityId, c.StationId, c.Slot }).HasName("pk_city_couriers");
        builder.Property(c => c.CityId).HasColumnName("city_id");
        builder.Property(c => c.StationId).HasColumnName("station_id").ValueGeneratedNever();
        builder.Property(c => c.Slot).HasColumnName("slot").ValueGeneratedNever();
        builder.Property(c => c.State).HasColumnName("state");
        builder.Property(c => c.ToStop).HasColumnName("to_stop");
        builder.Property(c => c.DepartedAt).HasColumnName("departed_at");
        builder.Property(c => c.ArrivesAt).HasColumnName("arrives_at");
        builder.Property(c => c.StaminaSeconds).HasColumnName("stamina_seconds");

        // The station owns its couriers: the rows go with it.
        builder.HasOne<CityBuildingRow>().WithMany().HasForeignKey(c => new { c.CityId, c.StationId })
            .HasConstraintName("fk_city_couriers_city_buildings_station_id").OnDelete(DeleteBehavior.Cascade);
    }
}
