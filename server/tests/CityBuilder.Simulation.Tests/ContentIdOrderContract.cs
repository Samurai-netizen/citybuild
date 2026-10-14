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

    [Theory]
    [InlineData("caf\u00E9", "cafe\u0301")]
    [InlineData("sawmill", "saw\u00ADmill")]
    public void CompareTo_TextsThatCulturesTreatAsEqual_IsNotZero(string first, string second)
    {
        Assert.NotEqual(0, IdOf(first).CompareTo(IdOf(second)));
    }

    [Fact]
    public void CompareTo_TextThatIsAPrefixOfAnother_SortsFirst()
    {
        Assert.True(IdOf("saw").CompareTo(IdOf("sawmill")) < 0);
        Assert.True(IdOf("sawmill").CompareTo(IdOf("saw")) > 0);
    }

    [Fact]
    public void CompareTo_EqualIds_IsZero()
    {
        Assert.Equal(0, IdOf("house").CompareTo(IdOf("house")));
    }

    [Fact]
    public void CompareTo_Default_SortsBeforeEveryId()
    {
        TId none = default;

        Assert.True(none.CompareTo(IdOf("a")) < 0);
        Assert.True(IdOf("a").CompareTo(none) > 0);
        Assert.Equal(0, none.CompareTo(default));
    }

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

    [Fact]
    public void Sort_OfIds_GivesOrdinalAscendingOrder()
    {
        TId[] ids = [IdOf("sawmill"), IdOf("house"), IdOf("woodcutter"), IdOf("Zebra"), IdOf("apple"), IdOf("saw")];

        Array.Sort(ids);

        Assert.Equal(["Zebra", "apple", "house", "saw", "sawmill", "woodcutter"], ids.Select(ValueOf));
    }

    [Fact]
    public void Declaration_IsComparableToItself()
    {
        Assert.True(typeof(IComparable<TId>).IsAssignableFrom(typeof(TId)));
    }
}
