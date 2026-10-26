namespace CityBuilder.Simulation.Tests;

/// <summary>
/// The ordering contract of the content ids that sort by their text (all but <see cref="EraId"/>): ordinal order, which never depends on
/// the machine's culture, with <c>default</c> first. It adds to the equality contract.
/// </summary>
/// <typeparam name="TId">The id type under test.</typeparam>
public abstract class ContentIdOrderContract<TId> : ContentIdEqualityContract<TId>
    where TId : struct, IEquatable<TId>, IComparable<TId>
{
    /// <summary>Applies the id's <c>&lt;</c> operator.</summary>
    protected abstract bool LessThanOperator(TId left, TId right);

    /// <summary>Applies the id's <c>&lt;=</c> operator.</summary>
    protected abstract bool LessThanOrEqualOperator(TId left, TId right);

    /// <summary>Applies the id's <c>&gt;</c> operator.</summary>
    protected abstract bool GreaterThanOperator(TId left, TId right);

    /// <summary>Applies the id's <c>&gt;=</c> operator.</summary>
    protected abstract bool GreaterThanOrEqualOperator(TId left, TId right);

    /// <summary>Ordinal order compares code units, so "B" (66) sorts before "a" (97), unlike a culture-aware comparison.</summary>
    [Fact]
    public void CompareTo_Ordinal_PutsUppercaseBeforeLowercase()
    {
        // Ordinal order compares code units: 'B' (66) comes before 'a' (97). A culture-aware comparison would put "a" first.
        TId upper = IdOf("B");
        TId lower = IdOf("a");

        Assert.True(upper.CompareTo(lower) < 0);
        Assert.True(lower.CompareTo(upper) > 0);
        Assert.True(LessThanOperator(upper, lower));
    }

    /// <summary>Texts that a culture-aware comparison calls equal don't compare as 0.</summary>
    [Theory]
    [InlineData("caf\u00E9", "cafe\u0301")]
    [InlineData("sawmill", "saw\u00ADmill")]
    public void CompareTo_TextsThatCulturesTreatAsEqual_IsNotZero(string first, string second)
    {
        Assert.NotEqual(0, IdOf(first).CompareTo(IdOf(second)));
    }

    /// <summary>A prefix sorts before the longer text: "saw" before "sawmill".</summary>
    [Fact]
    public void CompareTo_TextThatIsAPrefixOfAnother_SortsFirst()
    {
        Assert.True(IdOf("saw").CompareTo(IdOf("sawmill")) < 0);
        Assert.True(IdOf("sawmill").CompareTo(IdOf("saw")) > 0);
    }

    /// <summary>Equal ids compare as 0.</summary>
    [Fact]
    public void CompareTo_EqualIds_IsZero()
    {
        Assert.Equal(0, IdOf("house").CompareTo(IdOf("house")));
    }

    /// <summary><c>default</c> sorts before every real id and compares as 0 with another <c>default</c>.</summary>
    [Fact]
    public void CompareTo_Default_SortsBeforeEveryId()
    {
        TId none = default;

        Assert.True(none.CompareTo(IdOf("a")) < 0);
        Assert.True(IdOf("a").CompareTo(none) > 0);
        Assert.Equal(0, none.CompareTo(default));
    }

    /// <summary><c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> and <c>&gt;=</c> agree with <c>CompareTo</c> for smaller, equal and larger ids.</summary>
    [Fact]
    public void RelationalOperators_SmallerEqualAndLargerIds_FollowCompareTo()
    {
        TId smaller = IdOf("house");
        TId larger = IdOf("sawmill");
        TId sameAsSmaller = IdOf("house");

        Assert.True(LessThanOperator(smaller, larger));
        Assert.True(LessThanOrEqualOperator(smaller, larger));
        Assert.True(LessThanOrEqualOperator(smaller, sameAsSmaller));
        Assert.True(GreaterThanOperator(larger, smaller));
        Assert.True(GreaterThanOrEqualOperator(larger, smaller));
        Assert.True(GreaterThanOrEqualOperator(sameAsSmaller, smaller));
        Assert.False(LessThanOperator(larger, smaller));
        Assert.False(LessThanOperator(smaller, sameAsSmaller));
        Assert.False(LessThanOrEqualOperator(larger, smaller));
        Assert.False(GreaterThanOperator(smaller, larger));
        Assert.False(GreaterThanOperator(smaller, sameAsSmaller));
        Assert.False(GreaterThanOrEqualOperator(smaller, larger));
    }

    /// <summary>Sorting ids gives ordinal ascending order: uppercase first, prefixes before longer texts.</summary>
    [Fact]
    public void Sort_OfIds_GivesOrdinalAscendingOrder()
    {
        TId[] ids = [IdOf("sawmill"), IdOf("house"), IdOf("woodcutter"), IdOf("Zebra"), IdOf("apple"), IdOf("saw")];

        Array.Sort(ids);

        Assert.Equal(["Zebra", "apple", "house", "saw", "sawmill", "woodcutter"], ids.Select(ValueOf));
    }

    /// <summary>The id type implements <see cref="IComparable{T}"/> of itself, so sorting needs no boxing.</summary>
    [Fact]
    public void Declaration_IsComparableToItself()
    {
        Assert.True(typeof(IComparable<TId>).IsAssignableFrom(typeof(TId)));
    }
}
