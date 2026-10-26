using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityBuilder.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CityResearchRow"/> to <c>city_research</c> as core-instructions/06-data-model.md describes it.</summary>
public sealed class CityResearchConfiguration : IEntityTypeConfiguration<CityResearchRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CityResearchRow> builder)
    {
        builder.ToTable("city_research");

        builder.HasKey(r => new { r.CityId, r.ResearchId }).HasName("pk_city_research");
        builder.Property(r => r.CityId).HasColumnName("city_id");
        builder.Property(r => r.ResearchId).HasColumnName("research_id");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");

        // The city owns its research: the rows go with it.
        builder.HasOne<CityRow>().WithMany().HasForeignKey(r => r.CityId)
            .HasConstraintName("fk_city_research_cities_city_id").OnDelete(DeleteBehavior.Cascade);
    }
}
