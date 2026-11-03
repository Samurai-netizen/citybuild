using CityBuilder.Infrastructure.Persistence.Rows;

namespace CityBuilder.Infrastructure.Persistence.Mapping;

/// <summary>
/// One city as rows (<c>core-instructions/06-data-model.md</c>): its <c>cities</c> row and the rows of its child tables. What
/// <see cref="CityMapping"/> maps a <c>CityAggregate</c> to and from.
/// </summary>
/// <param name="city">The <c>cities</c> row.</param>
/// <param name="inventory">The <c>city_inventory</c> rows.</param>
/// <param name="buildings">The <c>city_buildings</c> rows.</param>
/// <param name="research">The <c>city_research</c> rows, completed research only.</param>
/// <param name="roads">The <c>city_roads</c> rows (CR-0008).</param>
public sealed class CityRows(
    CityRow city,
    IReadOnlyList<CityInventoryRow> inventory,
    IReadOnlyList<CityBuildingRow> buildings,
    IReadOnlyList<CityResearchRow> research,
    IReadOnlyList<CityRoadRow> roads)
{
    /// <summary>The <c>cities</c> row.</summary>
    public CityRow City { get; } = city ?? throw new ArgumentNullException(nameof(city));

    /// <summary>The <c>city_inventory</c> rows.</summary>
    public IReadOnlyList<CityInventoryRow> Inventory { get; } = inventory ?? throw new ArgumentNullException(nameof(inventory));

    /// <summary>The <c>city_buildings</c> rows.</summary>
    public IReadOnlyList<CityBuildingRow> Buildings { get; } = buildings ?? throw new ArgumentNullException(nameof(buildings));

    /// <summary>The <c>city_research</c> rows, completed research only.</summary>
    public IReadOnlyList<CityResearchRow> Research { get; } = research ?? throw new ArgumentNullException(nameof(research));

    /// <summary>The <c>city_roads</c> rows.</summary>
    public IReadOnlyList<CityRoadRow> Roads { get; } = roads ?? throw new ArgumentNullException(nameof(roads));
}
