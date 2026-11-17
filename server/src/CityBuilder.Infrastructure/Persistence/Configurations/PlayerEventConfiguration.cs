using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PlayerEventRow"/> to <c>player_events</c> as core-instructions/06-data-model.md describes it (CR-0009).</summary>
public sealed class PlayerEventConfiguration : IEntityTypeConfiguration<PlayerEventRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlayerEventRow> builder)
    {
        builder.ToTable("player_events", table => table.HasCheckConstraint("ck_player_events_city_clock", "city_clock >= 0"));

        builder.HasKey(e => e.Id).HasName("pk_player_events");
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(e => e.PlayerId).HasColumnName("player_id");
        builder.Property(e => e.EventType).HasColumnName("event_type");
        builder.Property(e => e.Detail).HasColumnName("detail");
        builder.Property(e => e.Outcome).HasColumnName("outcome");
        builder.Property(e => e.Value).HasColumnName("value");
        builder.Property(e => e.CityClock).HasColumnName("city_clock");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        // One player's timeline, and the players per event type and detail (the report, CR-0009-T2).
        builder.HasIndex(e => new { e.PlayerId, e.CreatedAt }).HasDatabaseName("ix_player_events_player_id_created_at");
        builder.HasIndex(e => new { e.EventType, e.Detail }).HasDatabaseName("ix_player_events_event_type_detail");
        // Analytics, not audit: unlike the ledger, a player's events go with the player.
        builder.HasOne<PlayerRow>().WithMany().HasForeignKey(e => e.PlayerId)
            .HasConstraintName("fk_player_events_players_player_id").OnDelete(DeleteBehavior.Cascade);
    }
}
