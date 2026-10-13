using System.Numerics;

namespace CityBuilder.Simulation.Tests;

/// <summary>Edge cases of the rounding-up division behind every duration of the engine.</summary>
public sealed class CeilDivTests
{
    // Values that reach the edges of long, used where a test checks the defining property with exact (BigInteger) arithmetic.
    private static readonly long[] _extremeValues =
    [
        1L, 2L, 3L, 7L, 60L, 180L, 1_000L, 999_999_999_999L, long.MaxValue / 2, long.MaxValue - 1, long.MaxValue,
    ];

    [Theory]
    [InlineData(0L, 1L)]
    [InlineData(0L, 7L)]
    [InlineData(0L, long.MaxValue)]
    public void CeilDiv_ZeroDividend_ReturnsZero(long dividend, long divisor)
    {
        Assert.Equal(0L, IntegerMath.CeilDiv(dividend, divisor));
    }

    [Theory]
    [InlineData(1L, 1L, 1L)]
    [InlineData(10L, 5L, 2L)]
    [InlineData(180L, 4L, 45L)] // 180 labor with 4 workers (the rounding example of M0-10)
    [InlineData(180L, 5L, 36L)] // 180 labor with 5 workers
    [InlineData(9_223_372_036_854_775_806L, 2L, 4_611_686_018_427_387_903L)] // the largest even dividend
    [InlineData(long.MaxValue, 1L, long.MaxValue)]
    [InlineData(long.MaxValue, long.MaxValue, 1L)]
    public void CeilDiv_ExactMultiple_ReturnsTheExactQuotient(long dividend, long divisor, long expected)
    {
        Assert.Equal(expected, IntegerMath.CeilDiv(dividend, divisor));
    }

    [Theory]
    [InlineData(1L, 2L, 1L)]
    [InlineData(3L, 2L, 2L)]
    [InlineData(7L, 2L, 4L)]
    [InlineData(181L, 4L, 46L)]
    [InlineData(179L, 5L, 36L)]
    [InlineData(1L, long.MaxValue, 1L)]
    [InlineData(long.MaxValue, 2L, 4_611_686_018_427_387_904L)] // (2^63 - 1) / 2 = 2^62 - 0.5
    [InlineData(long.MaxValue, 3L, 3_074_457_345_618_258_603L)]
    [InlineData(long.MaxValue, long.MaxValue - 1, 2L)]
    public void CeilDiv_NotAMultiple_RoundsUp(long dividend, long divisor, long expected)
    {
        Assert.Equal(expected, IntegerMath.CeilDiv(dividend, divisor));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(long.MaxValue)]
    public void CeilDiv_DivisorOne_ReturnsTheDividend(long dividend)
    {
        Assert.Equal(dividend, IntegerMath.CeilDiv(dividend, 1L));
    }

    [Theory]
    [InlineData(1L, 2L)]
    [InlineData(5L, 6L)]
    [InlineData(999L, 1_000L)]
    [InlineData(long.MaxValue - 1, long.MaxValue)]
    public void CeilDiv_PositiveDividendSmallerThanDivisor_ReturnsOne(long dividend, long divisor)
    {
        Assert.Equal(1L, IntegerMath.CeilDiv(dividend, divisor));
    }

    [Fact]
    public void CeilDiv_SmallRange_MatchesTheDefinitionOfTheCeiling()
    {
        var failures = new List<string>();

        for (long dividend = 0; dividend <= 400; dividend++)
        {
            for (long divisor = 1; divisor <= 70; divisor++)
            {
                long quotient = IntegerMath.CeilDiv(dividend, divisor);

                // The ceiling is the smallest quotient q with q * divisor >= dividend.
                bool bigEnough = quotient * divisor >= dividend;
                bool smallest = quotient == 0 ? dividend == 0 : (quotient - 1) * divisor < dividend;
                if (!bigEnough || !smallest)
                {
                    failures.Add($"CeilDiv({dividend}, {divisor}) = {quotient}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void CeilDiv_ExtremeValues_SatisfyTheDefinitionOfTheCeilingWithoutOverflow()
    {
        var failures = new List<string>();

        foreach (long dividend in _extremeValues)
        {
            foreach (long divisor in _extremeValues)
            {
                BigInteger quotient = IntegerMath.CeilDiv(dividend, divisor);

                bool bigEnough = quotient * divisor >= dividend;
                bool smallest = (quotient - 1) * divisor < dividend;
                if (!bigEnough || !smallest)
                {
                    failures.Add($"CeilDiv({dividend}, {divisor}) = {quotient}");
                }
            }
        }

        Assert.Empty(failures);
    }

    // Each bad argument is paired with several partners (edge values, and the other argument bad as well), so that no shortcut ahead of
    // the guards can let bad input through. When both are out of range the dividend is reported: it is checked first.
    [Theory]
    [InlineData(-1L, 1L)]
    [InlineData(-1L, long.MaxValue)]
    [InlineData(long.MinValue, 1L)]
    [InlineData(long.MinValue, long.MaxValue)]
    [InlineData(-1L, 0L)]
    [InlineData(-1L, -1L)]
    [InlineData(long.MinValue, long.MinValue)]
    public void CeilDiv_NegativeDividend_Throws(long dividend, long divisor)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.CeilDiv(dividend, divisor));

        Assert.Equal("dividend", exception.ParamName);
    }

    [Theory]
    [InlineData(10L, 0L)]
    [InlineData(0L, 0L)]
    [InlineData(0L, -1L)]
    [InlineData(1L, -1L)]
    [InlineData(long.MaxValue, 0L)]
    [InlineData(long.MaxValue, -1L)]
    [InlineData(0L, long.MinValue)]
    [InlineData(long.MaxValue, long.MinValue)]
    public void CeilDiv_ZeroOrNegativeDivisor_Throws(long dividend, long divisor)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.CeilDiv(dividend, divisor));

        Assert.Equal("divisor", exception.ParamName);
    }
}
