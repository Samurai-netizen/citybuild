using System.Reflection;

namespace CityBuilder.Simulation.Tests;

/// <summary>The text identity of an era: the shared equality contract, and no order of its own (eras are ordered by number).</summary>
public sealed class EraIdTests : ContentIdEqualityContract<EraId>
{
    /// <inheritdoc/>
    protected override EraId Create(string text) => new(text);

    /// <inheritdoc/>
    protected override string ValueOf(EraId id) => id.Value;

    /// <inheritdoc/>
    protected override bool EqualsOperator(EraId left, EraId right) => left == right;

    /// <inheritdoc/>
    protected override bool NotEqualsOperator(EraId left, EraId right) => left != right;

    /// <summary>An era id has no order at all (no IComparable, no relational operators), so no code can sort eras by text (decision 022).</summary>
    [Fact]
    public void Declaration_HasNoComparisonOrRelationalOperators()
    {
        // Decision 022: text order would put era_10 before era_2, so an era id must offer no order that code could sort eras by.
        string[] relationalOperators = ["op_LessThan", "op_LessThanOrEqual", "op_GreaterThan", "op_GreaterThanOrEqual"];

        Assert.False(typeof(IComparable).IsAssignableFrom(typeof(EraId)));
        Assert.False(typeof(IComparable<EraId>).IsAssignableFrom(typeof(EraId)));
        Assert.DoesNotContain(
            typeof(EraId).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => relationalOperators.Contains(method.Name));
    }
}
