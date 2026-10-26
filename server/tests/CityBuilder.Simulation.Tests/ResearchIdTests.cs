namespace CityBuilder.Simulation.Tests;

/// <summary>The identity of a research node: the shared equality and ordering contract of content ids.</summary>
public sealed class ResearchIdTests : ContentIdOrderContract<ResearchId>
{
    /// <inheritdoc/>
    protected override ResearchId Create(string text) => new(text);

    /// <inheritdoc/>
    protected override string ValueOf(ResearchId id) => id.Value;

    /// <inheritdoc/>
    protected override bool EqualsOperator(ResearchId left, ResearchId right) => left == right;

    /// <inheritdoc/>
    protected override bool NotEqualsOperator(ResearchId left, ResearchId right) => left != right;

    /// <inheritdoc/>
    protected override bool LessThanOperator(ResearchId left, ResearchId right) => left < right;

    /// <inheritdoc/>
    protected override bool LessThanOrEqualOperator(ResearchId left, ResearchId right) => left <= right;

    /// <inheritdoc/>
    protected override bool GreaterThanOperator(ResearchId left, ResearchId right) => left > right;

    /// <inheritdoc/>
    protected override bool GreaterThanOrEqualOperator(ResearchId left, ResearchId right) => left >= right;
}
