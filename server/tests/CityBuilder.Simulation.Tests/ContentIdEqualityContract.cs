namespace CityBuilder.Simulation.Tests;

/// <summary>
/// The equality contract that every typed content id shares (decision 022 in <c>core-instructions/12-decisions.md</c>): the text is kept as
/// given, equality is ordinal and never depends on the machine's culture, and <c>default</c> is the empty id. A test class per id type
/// derives from this and maps the members below to the id's own constructor and operators. <c>ItemIdTests</c> predates it and covers
/// <see cref="ItemId"/> on its own.
/// </summary>
/// <typeparam name="TId">The id type under test.</typeparam>
public abstract class ContentIdEqualityContract<TId>
    where TId : struct, IEquatable<TId>
{
    /// <summary>Calls the id's constructor.</summary>
    protected abstract TId Create(string text);

    /// <summary>Reads the id's <c>Value</c>.</summary>
    protected abstract string ValueOf(TId id);

    /// <summary>Applies the id's <c>==</c> operator.</summary>
    protected abstract bool EqualsOperator(TId left, TId right);

    /// <summary>Applies the id's <c>!=</c> operator.</summary>
    protected abstract bool NotEqualsOperator(TId left, TId right);

    /// <summary>The id of a text, built from a new string object each time, so a test can't pass by comparing references of interned literals.</summary>
    protected TId IdOf(string text) => Create(new string(text.ToCharArray()));

    /// <summary>A null text is rejected with <see cref="ArgumentNullException"/> for the parameter <c>value</c>.</summary>
    [Fact]
    public void Constructor_NullText_Throws()
    {
        // The null is deliberate: the nullable annotation on the parameter is the very contract under test.
        var exception = Assert.Throws<ArgumentNullException>(() => Create(null!));

        Assert.Equal("value", exception.ParamName);
    }

    /// <summary>An empty text is rejected with <see cref="ArgumentException"/>: the empty id is <c>default</c>, never a constructed one.</summary>
    [Fact]
    public void Constructor_EmptyText_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => Create(string.Empty));

        Assert.Equal("value", exception.ParamName);
    }

    /// <summary>The constructor neither trims nor normalizes, so " sawmill " is kept as given and differs from "sawmill".</summary>
    [Fact]
    public void Constructor_TextWithSurroundingWhitespace_KeepsTheTextAsGiven()
    {
        // Ids are equal when their text matches character by character, so the constructor must not trim or normalize.
        TId padded = IdOf(" sawmill ");

        Assert.Equal(" sawmill ", ValueOf(padded));
        Assert.False(EqualsOperator(padded, IdOf("sawmill")));
    }

    /// <summary><c>Value</c> and <c>ToString</c> both give the id's text.</summary>
    [Fact]
    public void ValueAndToString_IdWithText_ReturnThatText()
    {
        TId id = IdOf("trade_charter");

        Assert.Equal("trade_charter", ValueOf(id));
        Assert.Equal("trade_charter", id.ToString());
    }

    /// <summary>Two ids with the same text are equal through <c>Equals</c>, <c>Equals(object)</c> and <c>==</c>, and have the same hash code.</summary>
    [Fact]
    public void Equals_SameTextInSeparateStrings_IsEqualInEveryForm()
    {
        TId first = IdOf("early_medieval");
        TId second = IdOf("early_medieval");

        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.True(EqualsOperator(first, second));
        Assert.False(NotEqualsOperator(first, second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>Case matters: "sawmill" and "Sawmill" are different ids.</summary>
    [Fact]
    public void Equals_TextThatDiffersOnlyInCase_IsNotEqual()
    {
        TId lower = IdOf("sawmill");
        TId capitalized = IdOf("Sawmill");

        Assert.False(lower.Equals(capitalized));
        Assert.True(NotEqualsOperator(lower, capitalized));
    }

    /// <summary>Texts that a culture-aware comparison calls equal (composed and decomposed é, a soft hyphen) are different ids under ordinal equality.</summary>
    [Theory]
    [InlineData("caf\u00E9", "cafe\u0301")] // é as one character, and as e followed by a combining accent
    [InlineData("sawmill", "saw\u00ADmill")] // the same word with an invisible soft hyphen inside
    public void Equals_TextsThatCulturesTreatAsEqual_AreNotEqual(string first, string second)
    {
        // A culture-aware comparison calls these pairs equal; an ordinal one, which the engine requires, does not. ItemIdTests checks that
        // this machine has the culture data that makes the difference visible.
        TId left = IdOf(first);
        TId right = IdOf(second);

        Assert.False(left.Equals(right));
        Assert.True(NotEqualsOperator(left, right));
    }

    /// <summary>Ids with different texts are unequal through every form of equality.</summary>
    [Fact]
    public void Equals_DifferentText_IsNotEqualInEveryForm()
    {
        TId house = IdOf("house");
        TId sawmill = IdOf("sawmill");

        Assert.False(house.Equals(sawmill));
        Assert.False(house.Equals((object)sawmill));
        Assert.False(EqualsOperator(house, sawmill));
        Assert.True(NotEqualsOperator(house, sawmill));
    }

    /// <summary>An id equals neither null, nor a string, nor an id of another type with the same text: an item and a building type may share a name.</summary>
    [Fact]
    public void Equals_NullTextOrAnotherIdTypeWithTheSameText_IsFalse()
    {
        // An item id and a building type id may share their text, and they are still different things.
        TId id = IdOf("logs");

        Assert.False(id.Equals(null));
        Assert.False(id.Equals("logs"));
        Assert.False(id.Equals((object)new ItemId("logs")));
    }

    /// <summary><c>default</c> has the empty text, equals only another <c>default</c>, and hashes consistently.</summary>
    [Fact]
    public void Default_IsTheEmptyId_AndEqualsOnlyAnotherDefault()
    {
        TId none = default;

        Assert.Equal(string.Empty, ValueOf(none));
        Assert.Equal(string.Empty, none.ToString());
        Assert.True(EqualsOperator(none, default));
        Assert.Equal(default(TId).GetHashCode(), none.GetHashCode());
        Assert.False(EqualsOperator(none, IdOf("house")));
    }

    /// <summary>An id works as a dictionary key: another id with the same text finds the entry, one that differs in case does not.</summary>
    [Fact]
    public void DictionaryKey_IdBuiltFromAnotherString_IsFound()
    {
        var names = new Dictionary<TId, string> { [IdOf("house")] = "Cottage", [IdOf("sawmill")] = "Sawmill" };

        Assert.Equal("Cottage", names[IdOf("house")]);
        Assert.False(names.ContainsKey(IdOf("House")));
    }

    /// <summary>The id type is a readonly struct, so ids copy cheaply and can't change.</summary>
    [Fact]
    public void Declaration_IsAReadonlyStruct()
    {
        Assert.True(typeof(TId).IsValueType);
        Assert.Contains(typeof(TId).GetCustomAttributes(false), attribute => attribute.GetType().Name == "IsReadOnlyAttribute");
    }
}
