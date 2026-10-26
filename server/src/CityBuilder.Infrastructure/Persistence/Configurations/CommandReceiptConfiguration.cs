using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CommandReceiptRow"/> to <c>command_receipts</c> as core-instructions/06-data-model.md describes it.</summary>
public sealed class CommandReceiptConfiguration : IEntityTypeConfiguration<CommandReceiptRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CommandReceiptRow> builder)
    {
        builder.ToTable("command_receipts");

        builder.HasKey(r => new { r.PlayerId, r.RequestId }).HasName("pk_command_receipts");
        builder.Property(r => r.PlayerId).HasColumnName("player_id");
        builder.Property(r => r.RequestId).HasColumnName("request_id");
        builder.Property(r => r.CommandType).HasColumnName("command_type");
        builder.Property(r => r.PayloadHash).HasColumnName("payload_hash");
        builder.Property(r => r.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        // Receipts are part of the audit trail: a player with receipts can't be deleted.
        builder.HasOne<PlayerRow>().WithMany().HasForeignKey(r => r.PlayerId)
            .HasConstraintName("fk_command_receipts_players_player_id").OnDelete(DeleteBehavior.Restrict);
    }
}
