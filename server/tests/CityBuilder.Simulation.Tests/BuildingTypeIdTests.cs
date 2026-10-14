namespace CityBuilder.Simulation.Tests;

/// <summary>The identity of a building type: the shared equality and ordering contract of content ids.</summary>
public sealed class BuildingTypeIdTests : ContentIdOrderContract<BuildingTypeId>
{
    protected override BuildingTypeId Create(string text) => new(text);

    protected override string ValueOf(BuildingTypeId id) => id.Value;

    protected override bool EqualsOperator(BuildingTypeId left, BuildingTypeId right) => left == right;

    protected override bool NotEqualsOperator(BuildingTypeId left, BuildingTypeId right) => left != right;

    protected override bool LessThanOperator(BuildingTypeId left, BuildingTypeId right) => left < right;

    protected override bool LessThanOrEqualOperator(BuildingTypeId left, BuildingTypeId right) => left <= right;

    protected override bool GreaterThanOperator(BuildingTypeId left, BuildingTypeId right) => left > right;

    protected override bool GreaterThanOrEqualOperator(BuildingTypeId left, BuildingTypeId right) => left >= right;
}
