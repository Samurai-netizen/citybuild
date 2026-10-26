namespace CityBuilder.Simulation.Tests;

/// <summary>The identity of a building type: the shared equality and ordering contract of content ids.</summary>
public sealed class BuildingTypeIdTests : ContentIdOrderContract<BuildingTypeId>
{
    /// <inheritdoc/>
    protected override BuildingTypeId Create(string text) => new(text);

    /// <inheritdoc/>
    protected override string ValueOf(BuildingTypeId id) => id.Value;

    /// <inheritdoc/>
    protected override bool EqualsOperator(BuildingTypeId left, BuildingTypeId right) => left == right;

    /// <inheritdoc/>
    protected override bool NotEqualsOperator(BuildingTypeId left, BuildingTypeId right) => left != right;

    /// <inheritdoc/>
    protected override bool LessThanOperator(BuildingTypeId left, BuildingTypeId right) => left < right;

    /// <inheritdoc/>
    protected override bool LessThanOrEqualOperator(BuildingTypeId left, BuildingTypeId right) => left <= right;

    /// <inheritdoc/>
    protected override bool GreaterThanOperator(BuildingTypeId left, BuildingTypeId right) => left > right;

    /// <inheritdoc/>
    protected override bool GreaterThanOrEqualOperator(BuildingTypeId left, BuildingTypeId right) => left >= right;
}
