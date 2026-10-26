namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>A row of <c>city_research</c> (core-instructions/06-data-model.md): one research node a city completed.</summary>
public sealed class CityResearchRow
{
    /// <summary><c>city_id</c>: the city (<see cref="CityRow"/>); with <see cref="ResearchId"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>research_id</c>: the research node's content id.</summary>
    public string ResearchId { get; set; } = string.Empty;

    /// <summary><c>completed_at</c>: the city clock second the research completed.</summary>
    public long CompletedAt { get; set; }
}
