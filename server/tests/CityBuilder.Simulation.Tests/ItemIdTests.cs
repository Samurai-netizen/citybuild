using System.Globalization;

namespace CityBuilder.Simulation.Tests;

/// <summary>The identity of an item: text equality and ordering that never depend on the machine's culture.</summary>
public sealed class ItemIdTests
{
    /// <summary>The id of a text, built from a new string object each time, so a test can't pass by comparing references of interned literals.</summary>
    private static ItemId IdOf(string text) => new(new string(text.ToCharArray()));

    /// <summary>A null text is rejected with <see cref="ArgumentNullException"/> for the parameter <c>value</c>.</summary>
    [Fact]
    public void Constructor_NullText_Throws()
    {
        // The null is deliberate: the nullable annotation on the parameter is the very contract under test.
        var exception = Assert.Throws<ArgumentNullException>(() => new ItemId(null!));

        Assert.Equal("value", exception.ParamName);
    }

    /// <summary>An empty text is rejected: the empty id is <c>default</c>, never a constructed one.</summary>
    [Fact]
    public void Constructor_EmptyText_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ItemId(string.Empty));

        Assert.Equal("value", exception.ParamName);
    }

    /// <summary>The constructor neither trims nor normalizes, so " logs " is kept as given and differs from "logs".</summary>
    [Fact]
    public void Constructor_TextWithSurroundingWhitespace_KeepsTheTextAsGiven()
    {
        // Ids are equal when their text matches character by character, so the constructor must not trim or normalize.
        ItemId padded = IdOf(" logs ");

        Assert.Equal(" logs ", padded.Value);
        Assert.False(padded == IdOf("logs"));
    }

    /// <summary><c>Value</c> gives the id's text.</summary>
    [Fact]
    public void Value_IdWithText_ReturnsThatText()
    {
        Assert.Equal("trading_post", IdOf("trading_post").Value);
    }

    /// <summary><c>ToString</c> gives the id's text, as logs and messages show it.</summary>
    [Fact]
    public void ToString_IdWithText_ReturnsThatText()
    {
        Assert.Equal("planks", IdOf("planks").ToString());
    }

    /// <summary>Two ids with the same text are equal through <c>Equals</c>, <c>Equals(object)</c> and <c>==</c>, and have the same hash code.</summary>
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

    /// <summary>Case matters: "logs" and "Logs" are different items.</summary>
    [Fact]
    public void Equals_TextThatDiffersOnlyInCase_IsNotEqual()
    {
        ItemId lower = IdOf("logs");
        ItemId capitalized = IdOf("Logs");

        Assert.False(lower.Equals(capitalized));
        Assert.True(lower != capitalized);
    }

    /// <summary>Texts that a culture-aware comparison calls equal (composed and decomposed é, a soft hyphen) are different ids under ordinal equality.</summary>
    [Theory]
    [InlineData("café", "café")] // é as one character, and as e followed by a combining accent
    [InlineData("logs", "lo­gs")] // the same word with an invisible soft hyphen inside
    public void Equals_TextsThatCulturesTreatAsEqual_AreNotEqual(string first, string second)
    {
        // A culture-aware comparison calls these pairs equal; an ordinal one, which the engine requires, does not.
        ItemId left = IdOf(first);
        ItemId right = IdOf(second);

        Assert.False(left.Equals(right));
        Assert.True(left != right);
        Assert.NotEqual(0, left.CompareTo(right));
    }

    /// <summary>Ids with different texts are unequal through every form of equality.</summary>
    [Fact]
    public void Equals_DifferentText_IsNotEqualInEveryForm()
    {
        ItemId logs = IdOf("logs");
        ItemId planks = IdOf("planks");

        Assert.False(logs.Equals(planks));
        Assert.False(logs.Equals((object)planks));
        Assert.False(logs == planks);
        Assert.True(logs != planks);
    }

    /// <summary>An id equals neither null, nor its text as a string, nor a number.</summary>
    [Fact]
    public void Equals_NullOrAnotherType_IsFalse()
    {
        ItemId id = IdOf("logs");

        Assert.False(id.Equals(null));
        Assert.False(id.Equals("logs"));
        Assert.False(id.Equals(42));
    }

    /// <summary><c>default(ItemId)</c> has the empty text, equals only another <c>default</c>, and hashes consistently.</summary>
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

    /// <summary>Ordinal order compares code units, so "B" sorts before "a", unlike a culture-aware comparison.</summary>
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

    /// <summary>A prefix sorts before the longer text.</summary>
    [Fact]
    public void CompareTo_TextThatIsAPrefixOfAnother_SortsFirst()
    {
        Assert.True(IdOf("log").CompareTo(IdOf("logs")) < 0);
        Assert.True(IdOf("logs").CompareTo(IdOf("log")) > 0);
    }

    /// <summary>Equal ids compare as 0.</summary>
    [Fact]
    public void CompareTo_EqualIds_IsZero()
    {
        Assert.Equal(0, IdOf("coins").CompareTo(IdOf("coins")));
    }

    /// <summary><c>default</c> sorts before every real id.</summary>
    [Fact]
    public void CompareTo_Default_SortsBeforeEveryId()
    {
        ItemId none = default;

        Assert.True(none.CompareTo(IdOf("a")) < 0);
        Assert.True(IdOf("a").CompareTo(none) > 0);
        Assert.Equal(0, none.CompareTo(default));
    }

    /// <summary><c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> and <c>&gt;=</c> agree with <c>CompareTo</c> for smaller, equal and larger ids.</summary>
    [Fact]
    public void RelationalOperators_SmallerEqualAndLargerIds_FollowCompareTo()
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

    /// <summary>Sorting item ids gives ordinal ascending order, which is the order of every sorted list in the engine.</summary>
    [Fact]
    public void Sort_OfIds_GivesOrdinalAscendingOrder()
    {
        ItemId[] ids = [IdOf("logs"), IdOf("coins"), IdOf("planks"), IdOf("Zebra"), IdOf("apple"), IdOf("log")];

        Array.Sort(ids);

        Assert.Equal(["Zebra", "apple", "coins", "log", "logs", "planks"], ids.Select(id => id.Value));
    }

    /// <summary>An id works as a dictionary key: another id with the same text finds the entry, one that differs in case does not.</summary>
    [Fact]
    public void DictionaryKey_IdBuiltFromAnotherString_IsFound()
    {
        var stock = new Dictionary<ItemId, long> { [IdOf("logs")] = 30L, [IdOf("planks")] = 5L };

        Assert.Equal(30L, stock[IdOf("logs")]);
        Assert.False(stock.ContainsKey(IdOf("Logs")));
    }

    /// <summary><see cref="ItemId"/> is a readonly struct, so ids copy cheaply and can't change.</summary>
    [Fact]
    public void ItemId_Declaration_IsAReadonlyStruct()
    {
        Assert.True(typeof(ItemId).IsValueType);
        Assert.Contains(typeof(ItemId).GetCustomAttributes(false), attribute => attribute.GetType().Name == "IsReadOnlyAttribute");
    }

    /// <summary>The id implements <see cref="IEquatable{T}"/> and <see cref="IComparable{T}"/> of itself: without them every lookup would box it.</summary>
    [Fact]
    public void ItemId_Interfaces_IncludeEquatableAndComparableOfItself()
    {
        // Without IEquatable<ItemId>, every dictionary lookup would box the id and go through Equals(object).
        Assert.True(typeof(IEquatable<ItemId>).IsAssignableFrom(typeof(ItemId)));
        Assert.True(typeof(IComparable<ItemId>).IsAssignableFrom(typeof(ItemId)));
    }

    /// <summary>
    /// A precondition of the ordinal tests: this machine has culture data, so a culture-aware comparison really differs from an ordinal one. Under
    /// invariant globalization (no ICU) the two are identical, the ordinal tests would also pass for a culture-aware id, and this test fails loudly.
    /// </summary>
    [Fact]
    public void InvariantCulture_Comparison_DiffersFromOrdinal()
    {
        // Precondition of the ordinal tests above. They can only tell an ordinal comparison from a culture-aware one when this machine has
        // culture data. Under invariant globalization (no ICU) the two are identical and those tests would pass for a culture-aware
        // implementation too, so this test fails loudly instead.
        CompareInfo cultureAware = CultureInfo.InvariantCulture.CompareInfo;

        Assert.True(
            cultureAware.Compare("B", "a", CompareOptions.None) > 0,
            "No culture data: culture-aware comparison equals ordinal here, so the ordinal tests cannot detect a culture-aware ItemId.");
        Assert.True(
            cultureAware.Compare("café", "café", CompareOptions.None) == 0,
            "No culture data: canonically equivalent texts are not equal here, so the ordinal tests cannot detect a culture-aware ItemId.");
    }
}
