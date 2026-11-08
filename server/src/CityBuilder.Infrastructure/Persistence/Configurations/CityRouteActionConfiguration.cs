using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityRouteActionRow"/> to <c>city_route_actions</c> as core-instructions/06-data-model.md describes it (CR-0008).</summary>
public sealed class CityRouteActionConfiguration : IEntityTypeConfiguration<CityRouteActionRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityRouteActionRow> builder)
    {
        builder.ToTable("city_route_actions", table =>
        {
            table.HasCheckConstraint("ck_city_route_actions_slot", "slot >= 1");
            table.HasCheckConstraint("ck_city_route_actions_stop_index", "stop_index >= 0");
            table.HasCheckConstraint("ck_city_route_actions_action_index", "action_index >= 0");
            table.HasCheckConstraint("ck_city_route_actions_kind", "kind in ('take', 'put')");
        });

        builder.HasKey(a => new { a.CityId, a.StationId, a.Slot, a.StopIndex, a.ActionIndex }).HasName("pk_city_route_actions");
        builder.Property(a => a.CityId).HasColumnName("city_id");
        builder.Property(a => a.StationId).HasColumnName("station_id").ValueGeneratedNever();
        builder.Property(a => a.Slot).HasColumnName("slot").ValueGeneratedNever();
        builder.Property(a => a.StopIndex).HasColumnName("stop_index").ValueGeneratedNever();
        builder.Property(a => a.ActionIndex).HasColumnName("action_index").ValueGeneratedNever();
        builder.Property(a => a.BuildingId).HasColumnName("building_id");
        builder.Property(a => a.Kind).HasColumnName("kind");
        builder.Property(a => a.ItemId).HasColumnName("item_id");
        builder.Property(a => a.Amount).HasColumnName("amount");

        // The station owns its routes: the rows go with it.
        builder.HasOne<CityBuildingRow>().WithMany().HasForeignKey(a => new { a.CityId, a.StationId })
            .HasConstraintName("fk_city_route_actions_city_buildings_station_id").OnDelete(DeleteBehavior.Cascade);
    }
}
