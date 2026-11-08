using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>
/// <see cref="Amount"/> (<c>04</c> section 19, <c>05</c> → Amounts): the three kinds, their ranges, the API's text form both ways, and equality.
/// </summary>
public sealed class AmountTests
{
    /// <summary>Text of the content-id form is a preset: no mode, the id, nothing else.</summary>
    [Fact]
    public void TryParse_PresetId_IsAPreset()
    {
        Assert.True(Amount.TryParse("half", out Amount? amount));

        Assert.Equal(((AmountMode?)null, "half", 0L), (amount.Mode, amount.PresetId.Value, amount.Value));
    }

    /// <summary>A percentage is 1 % to 100 %, written without a leading zero.</summary>
    /// <param name="text">The text.</param>
    /// <param name="percent">The percentage it gives.</param>
    [Theory]
    [InlineData("1%", 1)]
    [InlineData("50%", 50)]
    [InlineData("100%", 100)]
    public void TryParse_Percentage_IsAPercentAmount(string text, long percent)
    {
        Assert.True(Amount.TryParse(text, out Amount? amount));

        Assert.Equal((AmountMode.Percent, percent), (amount.Mode, amount.Value));
    }

    /// <summary>An exact number is 1 to 12 digits without a leading zero.</summary>
    /// <param name="text">The text.</param>
    /// <param name="quantity">The number it gives.</param>
    [Theory]
    [InlineData("1", 1L)]
    [InlineData("12", 12L)]
    [InlineData("999999999999", 999_999_999_999L)]
    public void TryParse_Number_IsAnExactAmount(string text, long quantity)
    {
        Assert.True(Amount.TryParse(text, out Amount? amount));

        Assert.Equal((AmountMode.Exact, quantity), (amount.Mode, amount.Value));
    }

    /// <summary>Any other text is no amount: empty, null, out of range, leading zeros, signs, spaces, decimals, capitals.</summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0%")]
    [InlineData("101%")]
    [InlineData("05%")]
    [InlineData("%")]
    [InlineData("1.5%")]
    [InlineData("-1%")]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("1000000000000")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1e3")]
    [InlineData("Half")]
    [InlineData("half%")]
    public void TryParse_OtherText_IsNoAmount(string? text)
    {
        Assert.False(Amount.TryParse(text, out Amount? amount));
        Assert.Null(amount);
    }

    /// <summary>The text form reads back to an equal amount, for each kind.</summary>
    /// <param name="text">The API form.</param>
    [Theory]
    [InlineData("all")]
    [InlineData("25%")]
    [InlineData("7")]
    public void ToString_ReadBack_GivesAnEqualAmount(string text)
    {
        Assert.True(Amount.TryParse(text, out Amount? amount));

        Assert.Equal(text, amount.ToString());
        Assert.True(Amount.TryParse(amount.ToString(), out Amount? again));
        Assert.Equal(amount, again);
        Assert.Equal(amount.GetHashCode(), again.GetHashCode());
    }

    /// <summary>The factories give the same amounts as the text, and amounts of different kinds or values differ.</summary>
    [Fact]
    public void Equals_SameKindAndValue_AreEqual()
    {
        Assert.Equal(Amount.Percent(50), Amount.TryParse("50%", out Amount? percent) ? percent : null);
        Assert.Equal(Amount.Exact(50), Amount.TryParse("50", out Amount? exact) ? exact : null);
        Assert.Equal(Amount.Preset(new AmountPresetId("all")), Amount.TryParse("all", out Amount? preset) ? preset : null);
        Assert.NotEqual(Amount.Percent(50), Amount.Exact(50));
        Assert.NotEqual(Amount.Percent(50), Amount.Percent(51));
        Assert.NotEqual(Amount.Preset(new AmountPresetId("all")), Amount.Preset(new AmountPresetId("half")));
        Assert.False(Amount.Exact(1).Equals(null));
    }

    /// <summary>A percentage outside 1..100, an exact number outside 1..999,999,999,999 and a preset without an id are programmer errors.</summary>
    [Fact]
    public void Factories_ValuesOutOfRange_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Amount.Percent(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Amount.Percent(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => Amount.Exact(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Amount.Exact(Amount.MaxExact + 1));
        Assert.Throws<ArgumentException>(() => Amount.Preset(default));
    }
}
