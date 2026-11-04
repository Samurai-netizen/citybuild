using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityResearchNeedRow"/> to <c>city_research_needs</c> as core-instructions/06-data-model.md describes it (CR-0008).</summary>
public sealed class CityResearchNeedConfiguration : IEntityTypeConfiguration<CityResearchNeedRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityResearchNeedRow> builder)
    {
        builder.ToTable("city_research_needs", table => table.HasCheckConstraint("ck_city_research_needs_quantity", "quantity > 0"));

        builder.HasKey(n => new { n.CityId, n.ItemId }).HasName("pk_city_research_needs");
        builder.Property(n => n.CityId).HasColumnName("city_id");
        builder.Property(n => n.ItemId).HasColumnName("item_id");
        builder.Property(n => n.Quantity).HasColumnName("quantity");

        // The city owns its research: the rows go with it.
        builder.HasOne<CityRow>().WithMany().HasForeignKey(n => n.CityId)
            .HasConstraintName("fk_city_research_needs_cities_city_id").OnDelete(DeleteBehavior.Cascade);
    }
}
