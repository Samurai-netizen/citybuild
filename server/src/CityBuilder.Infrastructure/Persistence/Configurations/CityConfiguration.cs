using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityRow"/> to <c>cities</c> as core-instructions/06-data-model.md describes it.</summary>
public sealed class CityConfiguration : IEntityTypeConfiguration<CityRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityRow> builder)
    {
        builder.ToTable("cities", table => table.HasCheckConstraint("ck_cities_era", "era >= 1"));

        builder.HasKey(c => c.Id).HasName("pk_cities");
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.PlayerId).HasColumnName("player_id");
        builder.Property(c => c.Era).HasColumnName("era");
        builder.Property(c => c.ClockSeconds).HasColumnName("clock_seconds");
        builder.Property(c => c.LastSettledUnix).HasColumnName("last_settled_unix");
        builder.Property(c => c.NextBuildingId).HasColumnName("next_building_id");
        builder.Property(c => c.ResearchId).HasColumnName("research_id");
        builder.Property(c => c.ResearchStartedAt).HasColumnName("research_started_at");
        builder.Property(c => c.ResearchEndsAt).HasColumnName("research_ends_at");
        builder.Property(c => c.StateVersion).HasColumnName("state_version").IsConcurrencyToken();
        builder.Property(c => c.ContentVersion).HasColumnName("content_version");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        // One city per player.
        builder.HasIndex(c => c.PlayerId).IsUnique().HasDatabaseName("ux_cities_player_id");
        builder.HasOne<PlayerRow>().WithMany().HasForeignKey(c => c.PlayerId)
            .HasConstraintName("fk_cities_players_player_id").OnDelete(DeleteBehavior.Restrict);
    }
}
