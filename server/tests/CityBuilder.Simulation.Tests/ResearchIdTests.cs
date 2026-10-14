namespace CityBuilder.Simulation.Tests;

/// <summary>The identity of a research node: the shared equality and ordering contract of content ids.</summary>
public sealed class ResearchIdTests : ContentIdOrderContract<ResearchId>
{
    protected override ResearchId Create(string text) => new(text);

    protected override string ValueOf(ResearchId id) => id.Value;

    protected override bool EqualsOperator(ResearchId left, ResearchId right) => left == right;

    protected override bool NotEqualsOperator(ResearchId left, ResearchId right) => left != right;

    protected override bool LessThanOperator(ResearchId left, ResearchId right) => left < right;

    protected override bool LessThanOrEqualOperator(ResearchId left, ResearchId right) => left <= right;

    protected override bool GreaterThanOperator(ResearchId left, ResearchId right) => left > right;

    protected override bool GreaterThanOrEqualOperator(ResearchId left, ResearchId right) => left >= right;
}
