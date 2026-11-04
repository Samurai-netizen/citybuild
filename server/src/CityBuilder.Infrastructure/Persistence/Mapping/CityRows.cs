using CityBuilder.Infrastructure.Persistence.Rows;

namespace CityBuilder.Infrastructure.Persistence.Mapping;

/// <summary>
/// One city as rows (<c>core-instructions/06-data-model.md</c>): its <c>cities</c> row and the rows of its child tables. What
/// <see cref="CityMapping"/> maps a <c>CityAggregate</c> to and from.
/// </summary>
/// <param name="city">The <c>cities</c> row.</param>
/// <param name="buildings">The <c>city_buildings</c> rows.</param>
/// <param name="stores">The <c>city_stores</c> rows (CR-0008).</param>
/// <param name="projectNeeds">The <c>city_project_needs</c> rows (CR-0008).</param>
/// <param name="research">The <c>city_research</c> rows, completed research only.</param>
/// <param name="researchNeeds">The <c>city_research_needs</c> rows (CR-0008).</param>
/// <param name="roads">The <c>city_roads</c> rows (CR-0008).</param>
/// <param name="trips">The <c>city_trips</c> rows (CR-0008).</param>
public sealed class CityRows(
    CityRow city,
    IReadOnlyList<CityBuildingRow> buildings,
    IReadOnlyList<CityStoreRow> stores,
    IReadOnlyList<CityProjectNeedRow> projectNeeds,
    IReadOnlyList<CityResearchRow> research,
    IReadOnlyList<CityResearchNeedRow> researchNeeds,
    IReadOnlyList<CityRoadRow> roads,
    IReadOnlyList<CityTripRow> trips)
{
    /// <summary>The <c>cities</c> row.</summary>
    public CityRow City { get; } = city ?? throw new ArgumentNullException(nameof(city));

    /// <summary>The <c>city_buildings</c> rows.</summary>
    public IReadOnlyList<CityBuildingRow> Buildings { get; } = buildings ?? throw new ArgumentNullException(nameof(buildings));

    /// <summary>The <c>city_stores</c> rows.</summary>
    public IReadOnlyList<CityStoreRow> Stores { get; } = stores ?? throw new ArgumentNullException(nameof(stores));

    /// <summary>The <c>city_project_needs</c> rows.</summary>
    public IReadOnlyList<CityProjectNeedRow> ProjectNeeds { get; } = projectNeeds ?? throw new ArgumentNullException(nameof(projectNeeds));

    /// <summary>The <c>city_research</c> rows, completed research only.</summary>
    public IReadOnlyList<CityResearchRow> Research { get; } = research ?? throw new ArgumentNullException(nameof(research));

    /// <summary>The <c>city_research_needs</c> rows.</summary>
    public IReadOnlyList<CityResearchNeedRow> ResearchNeeds { get; } = researchNeeds ?? throw new ArgumentNullException(nameof(researchNeeds));

    /// <summary>The <c>city_roads</c> rows.</summary>
    public IReadOnlyList<CityRoadRow> Roads { get; } = roads ?? throw new ArgumentNullException(nameof(roads));

    /// <summary>The <c>city_trips</c> rows.</summary>
    public IReadOnlyList<CityTripRow> Trips { get; } = trips ?? throw new ArgumentNullException(nameof(trips));
}
