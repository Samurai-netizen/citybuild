using System.Numerics;

namespace CityBuilder.Simulation.Tests;

/// <summary>Edge cases of the rounding-down division that the display helpers use for rates and progress.</summary>
public sealed class FloorDivTests
{
    [Theory]
    [InlineData(0L, 1L)]
    [InlineData(0L, 7L)]
    [InlineData(0L, long.MaxValue)]
    public void FloorDiv_ZeroDividend_ReturnsZero(long dividend, long divisor)
    {
        Assert.Equal(0L, IntegerMath.FloorDiv(dividend, divisor));
    }

    [Theory]
    [InlineData(10L, 5L, 2L)]
    [InlineData(180L, 4L, 45L)]
    [InlineData(long.MaxValue, 1L, long.MaxValue)]
    [InlineData(long.MaxValue, long.MaxValue, 1L)]
    public void FloorDiv_ExactMultiple_ReturnsTheExactQuotient(long dividend, long divisor, long expected)
    {
        Assert.Equal(expected, IntegerMath.FloorDiv(dividend, divisor));
    }

    [Theory]
    [InlineData(1L, 2L, 0L)]
    [InlineData(3L, 2L, 1L)]
    [InlineData(7L, 2L, 3L)]
    [InlineData(181L, 4L, 45L)]
    [InlineData(1L, long.MaxValue, 0L)]
    [InlineData(long.MaxValue, 2L, 4_611_686_018_427_387_903L)]
    [InlineData(long.MaxValue, long.MaxValue - 1, 1L)]
    public void FloorDiv_NotAMultiple_RoundsDown(long dividend, long divisor, long expected)
    {
        Assert.Equal(expected, IntegerMath.FloorDiv(dividend, divisor));
    }

    [Fact]
    public void FloorDiv_SmallRange_MatchesTheDefinitionOfTheFloor()
    {
        var failures = new List<string>();

        for (long dividend = 0; dividend <= 400; dividend++)
        {
            for (long divisor = 1; divisor <= 70; divisor++)
            {
                BigInteger quotient = IntegerMath.FloorDiv(dividend, divisor);

                // The floor is the largest quotient q with q * divisor <= dividend.
                if (quotient * divisor > dividend || (quotient + 1) * divisor <= dividend)
                {
                    failures.Add($"FloorDiv({dividend}, {divisor}) = {quotient}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData(-1L, 1L)]
    [InlineData(long.MinValue, long.MaxValue)]
    [InlineData(-1L, 0L)]
    [InlineData(-1L, -1L)]
    public void FloorDiv_NegativeDividend_Throws(long dividend, long divisor)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.FloorDiv(dividend, divisor));

        Assert.Equal("dividend", exception.ParamName);
    }

    [Theory]
    [InlineData(10L, 0L)]
    [InlineData(0L, 0L)]
    [InlineData(1L, -1L)]
    [InlineData(long.MaxValue, long.MinValue)]
    public void FloorDiv_ZeroOrNegativeDivisor_Throws(long dividend, long divisor)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.FloorDiv(dividend, divisor));

        Assert.Equal("divisor", exception.ParamName);
    }
}
