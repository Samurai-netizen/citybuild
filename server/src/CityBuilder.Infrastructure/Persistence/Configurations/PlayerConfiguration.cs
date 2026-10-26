using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PlayerRow"/> to <c>players</c> as core-instructions/06-data-model.md describes it.</summary>
public sealed class PlayerConfiguration : IEntityTypeConfiguration<PlayerRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlayerRow> builder)
    {
        builder.ToTable("players");

        builder.HasKey(p => p.Id).HasName("pk_players");
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.DevName).HasColumnName("dev_name");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(p => p.DevName).IsUnique().HasDatabaseName("ux_players_dev_name");
    }
}
