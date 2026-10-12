namespace CityBuilder.Simulation.Tests;

/// <summary>The identity of an item: text equality and ordering that never depend on the machine's culture.</summary>
public sealed class ItemIdTests
{
    // A new string object each time, so a test cannot pass by comparing references of interned literals.
    private static ItemId IdOf(string text) => new(new string(text.ToCharArray()));

    [Fact]
    public void Constructor_NullText_Throws()
    {
        // The null is deliberate: the nullable annotation on the parameter is the very contract under test.
        var exception = Assert.Throws<ArgumentNullException>(() => new ItemId(null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Constructor_EmptyText_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ItemId(string.Empty));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Value_ReturnsTheTextTheIdWasCreatedWith()
    {
        Assert.Equal("trading_post", IdOf("trading_post").Value);
    }

    [Fact]
    public void ToString_ReturnsTheText()
    {
        Assert.Equal("planks", IdOf("planks").ToString());
    }

    [Fact]
    public void Equals_SameTextInSeparateStrings_IsEqualInEveryForm()
    {
        ItemId first = IdOf("logs");
        ItemId second = IdOf("logs");

        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_TextThatDiffersOnlyInCase_IsNotEqual()
    {
        ItemId lower = IdOf("logs");
        ItemId capitalized = IdOf("Logs");

        Assert.False(lower.Equals(capitalized));
        Assert.True(lower != capitalized);
    }

    [Fact]
    public void Equals_DifferentText_IsNotEqual()
    {
        Assert.False(IdOf("logs") == IdOf("planks"));
        Assert.True(IdOf("logs") != IdOf("planks"));
    }

    [Fact]
    public void Equals_NullOrAnotherType_IsFalse()
    {
        ItemId id = IdOf("logs");

        Assert.False(id.Equals(null));
        Assert.False(id.Equals("logs"));
        Assert.False(id.Equals(42));
    }

    [Fact]
    public void Default_IsTheEmptyId_AndEqualsOnlyAnotherDefault()
    {
        ItemId none = default;

        Assert.Equal(string.Empty, none.Value);
        Assert.Equal(string.Empty, none.ToString());
        Assert.True(none == default(ItemId));
        Assert.Equal(default(ItemId).GetHashCode(), none.GetHashCode());
        Assert.False(none == IdOf("coins"));
    }

    [Fact]
    public void CompareTo_Ordinal_PutsUppercaseBeforeLowercase()
    {
        // Ordinal order compares code units: 'B' (66) comes before 'a' (97). A culture-aware comparison would put "a" first.
        ItemId upper = IdOf("B");
        ItemId lower = IdOf("a");

        Assert.True(upper.CompareTo(lower) < 0);
        Assert.True(lower.CompareTo(upper) > 0);
        Assert.True(upper < lower);
    }

    [Fact]
    public void CompareTo_TextThatIsAPrefixOfAnother_SortsFirst()
    {
        Assert.True(IdOf("log").CompareTo(IdOf("logs")) < 0);
        Assert.True(IdOf("logs").CompareTo(IdOf("log")) > 0);
    }

    [Fact]
    public void CompareTo_EqualIds_IsZero()
    {
        Assert.Equal(0, IdOf("coins").CompareTo(IdOf("coins")));
    }

    [Fact]
    public void CompareTo_Default_SortsBeforeEveryId()
    {
        ItemId none = default;

        Assert.True(none.CompareTo(IdOf("a")) < 0);
        Assert.True(IdOf("a").CompareTo(none) > 0);
        Assert.Equal(0, none.CompareTo(default));
    }

    [Fact]
    public void RelationalOperators_FollowCompareTo()
    {
        ItemId smaller = IdOf("coins");
        ItemId larger = IdOf("logs");
        ItemId sameAsSmaller = IdOf("coins");

        Assert.True(smaller < larger);
        Assert.True(smaller <= larger);
        Assert.True(smaller <= sameAsSmaller);
        Assert.True(larger > smaller);
        Assert.True(larger >= smaller);
        Assert.True(sameAsSmaller >= smaller);
        Assert.False(larger < smaller);
        Assert.False(smaller < sameAsSmaller);
        Assert.False(larger <= smaller);
        Assert.False(smaller > larger);
        Assert.False(smaller > sameAsSmaller);
        Assert.False(smaller >= larger);
    }

    [Fact]
    public void Sort_OfIds_GivesOrdinalAscendingOrder()
    {
        ItemId[] ids = [IdOf("logs"), IdOf("coins"), IdOf("planks"), IdOf("Zebra"), IdOf("apple"), IdOf("log")];

        Array.Sort(ids);

        Assert.Equal(["Zebra", "apple", "coins", "log", "logs", "planks"], ids.Select(id => id.Value));
    }

    [Fact]
    public void DictionaryKey_FindsAnIdBuiltFromAnotherString()
    {
        var stock = new Dictionary<ItemId, long> { [IdOf("logs")] = 30L, [IdOf("planks")] = 5L };

        Assert.Equal(30L, stock[IdOf("logs")]);
        Assert.False(stock.ContainsKey(IdOf("Logs")));
    }

    [Fact]
    public void ItemId_IsDeclaredAsAReadonlyStruct()
    {
        Assert.True(typeof(ItemId).IsValueType);
        Assert.Contains(typeof(ItemId).GetCustomAttributes(false), attribute => attribute.GetType().Name == "IsReadOnlyAttribute");
    }
}
