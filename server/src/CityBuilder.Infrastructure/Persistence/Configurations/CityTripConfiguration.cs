using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityTripRow"/> to <c>city_trips</c> as core-instructions/06-data-model.md describes it (CR-0008).</summary>
public sealed class CityTripConfiguration : IEntityTypeConfiguration<CityTripRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityTripRow> builder)
    {
        builder.ToTable("city_trips", table =>
        {
            table.HasCheckConstraint(
                "ck_city_trips_quantity", "(leg = 'ToTarget' and quantity > 0) or (leg = 'ToSource' and quantity = 0 and remaining > 0)");
            table.HasCheckConstraint("ck_city_trips_remaining", "remaining >= 0");
            table.HasCheckConstraint("ck_city_trips_arrives_at", "arrives_at > departed_at");
        });

        builder.HasKey(t => new { t.CityId, t.TripId }).HasName("pk_city_trips");
        builder.Property(t => t.CityId).HasColumnName("city_id");
        builder.Property(t => t.TripId).HasColumnName("trip_id").ValueGeneratedNever();
        builder.Property(t => t.SourceId).HasColumnName("source_id");
        builder.Property(t => t.TargetId).HasColumnName("target_id");
        builder.Property(t => t.ItemId).HasColumnName("item_id");
        builder.Property(t => t.Quantity).HasColumnName("quantity");
        builder.Property(t => t.DepartedAt).HasColumnName("departed_at");
        builder.Property(t => t.ArrivesAt).HasColumnName("arrives_at");
        builder.Property(t => t.Remaining).HasColumnName("remaining");
        builder.Property(t => t.Leg).HasColumnName("leg");

        // The city owns its trips: the rows go with it.
        builder.HasOne<CityRow>().WithMany().HasForeignKey(t => t.CityId)
            .HasConstraintName("fk_city_trips_cities_city_id").OnDelete(DeleteBehavior.Cascade);
    }
}
