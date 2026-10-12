using System.Numerics;

namespace CityBuilder.Simulation.Tests;

/// <summary>Edge cases of the integer helpers behind every duration and labor calculation of the engine.</summary>
public sealed class IntegerMathTests
{
    // Values that reach the edges of long, used where a test checks the defining property with exact (BigInteger) arithmetic.
    private static readonly long[] _extremeValues =
    [
        1L, 2L, 3L, 7L, 60L, 180L, 1_000L, 999_999_999_999L, long.MaxValue / 2, long.MaxValue - 1, long.MaxValue,
    ];

    // ----- CeilDiv -----

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

    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void CeilDiv_NegativeDividend_Throws(long dividend)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.CeilDiv(dividend, 1L));

        Assert.Equal("dividend", exception.ParamName);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void CeilDiv_ZeroOrNegativeDivisor_Throws(long divisor)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.CeilDiv(10L, divisor));

        Assert.Equal("divisor", exception.ParamName);
    }

    // ----- ApplyPermilleReduction -----

    [Fact]
    public void PermilleScale_IsOneThousand()
    {
        Assert.Equal(1_000, IntegerMath.PermilleScale);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(60L)]
    [InlineData(180L)]
    [InlineData(long.MaxValue)]
    public void ApplyPermilleReduction_ZeroPermille_ReturnsTheLaborUnchanged(long labor)
    {
        Assert.Equal(labor, IntegerMath.ApplyPermilleReduction(labor, 0));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(180L)]
    [InlineData(long.MaxValue)]
    public void ApplyPermilleReduction_AllPermille_ReturnsZero(long labor)
    {
        Assert.Equal(0L, IntegerMath.ApplyPermilleReduction(labor, 1_000));
    }

    [Fact]
    public void ApplyPermilleReduction_ZeroLabor_ReturnsZeroForEveryPermille()
    {
        for (int permille = 0; permille <= 1_000; permille++)
        {
            Assert.Equal(0L, IntegerMath.ApplyPermilleReduction(0L, permille));
        }
    }

    [Theory]
    [InlineData(180L, 500, 90L)] // the largest reduction in the M0 content (500 permille)
    [InlineData(180L, 250, 135L)]
    [InlineData(60L, 100, 54L)]
    [InlineData(120L, 200, 96L)]
    [InlineData(1_000L, 500, 500L)]
    [InlineData(1_000L, 1, 999L)]
    public void ApplyPermilleReduction_ExactResult_IsNotRoundedUp(long labor, int permille, long expected)
    {
        Assert.Equal(expected, IntegerMath.ApplyPermilleReduction(labor, permille));
    }

    [Theory]
    [InlineData(100L, 333, 67L)] // 66.7
    [InlineData(1L, 1, 1L)] // 0.999
    [InlineData(1L, 999, 1L)] // 0.001: a little labor never rounds down to none
    [InlineData(999L, 1, 999L)] // 998.001
    [InlineData(1_001L, 1, 1_000L)] // 999.999
    [InlineData(7L, 500, 4L)] // 3.5
    [InlineData(3L, 333, 3L)] // 2.001
    [InlineData(1_999L, 500, 1_000L)] // 999.5
    public void ApplyPermilleReduction_FractionalResult_RoundsUp(long labor, int permille, long expected)
    {
        Assert.Equal(expected, IntegerMath.ApplyPermilleReduction(labor, permille));
    }

    [Fact]
    public void ApplyPermilleReduction_SmallRange_MatchesTheDefinitionOfTheCeiling()
    {
        var failures = new List<string>();

        for (long labor = 0; labor <= 1_500; labor++)
        {
            for (int permille = 0; permille <= 1_000; permille++)
            {
                long reduced = IntegerMath.ApplyPermilleReduction(labor, permille);
                long exactTimesThousand = labor * (1_000 - permille);

                // The ceiling of exactTimesThousand / 1000 is the smallest r with r * 1000 >= exactTimesThousand.
                bool bigEnough = reduced * 1_000 >= exactTimesThousand;
                bool smallest = reduced == 0 ? exactTimesThousand == 0 : (reduced - 1) * 1_000 < exactTimesThousand;
                bool notMoreThanLabor = reduced <= labor;
                if (!bigEnough || !smallest || !notMoreThanLabor)
                {
                    failures.Add($"ApplyPermilleReduction({labor}, {permille}) = {reduced}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void ApplyPermilleReduction_LaborUpToLongMaxValue_IsExactAndDoesNotOverflow()
    {
        // Above long.MaxValue / 1000 (about 9.2e15) the product labor * (1000 - permille) no longer fits in a long.
        long[] labors =
        [
            long.MaxValue, long.MaxValue - 1, long.MaxValue - 999, 9_223_372_036_854_775_000L,
            1_000_000_000_000_000L, 10_000_000_000_000_000L, 1_000_000_000_000_000_000L,
        ];
        int[] permilles = [0, 1, 250, 333, 500, 999, 1_000];
        var failures = new List<string>();

        foreach (long labor in labors)
        {
            foreach (int permille in permilles)
            {
                BigInteger expected = ((new BigInteger(labor) * (1_000 - permille)) + 999) / 1_000;
                long reduced = IntegerMath.ApplyPermilleReduction(labor, permille);
                if (reduced != expected)
                {
                    failures.Add($"ApplyPermilleReduction({labor}, {permille}) = {reduced}, expected {expected}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(7L)]
    [InlineData(180L)]
    [InlineData(1_001L)]
    [InlineData(1_000_000_000L)]
    [InlineData(long.MaxValue)]
    public void ApplyPermilleReduction_MorePermille_NeverGivesMoreLabor(long labor)
    {
        long previous = IntegerMath.ApplyPermilleReduction(labor, 0);

        for (int permille = 1; permille <= 1_000; permille++)
        {
            long current = IntegerMath.ApplyPermilleReduction(labor, permille);
            Assert.True(current <= previous, $"labor {labor}: {permille} permille gives {current}, more than {previous}");
            previous = current;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(333)]
    [InlineData(500)]
    [InlineData(1_000)]
    public void ApplyPermilleReduction_MoreLabor_NeverGivesLessLabor(int permille)
    {
        long previous = IntegerMath.ApplyPermilleReduction(0L, permille);

        for (long labor = 1; labor <= 2_000; labor++)
        {
            long current = IntegerMath.ApplyPermilleReduction(labor, permille);
            Assert.True(current >= previous, $"{permille} permille: labor {labor} gives {current}, less than {previous}");
            previous = current;
        }
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void ApplyPermilleReduction_NegativeLabor_Throws(long labor)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.ApplyPermilleReduction(labor, 0));

        Assert.Equal("labor", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_001)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ApplyPermilleReduction_PermilleOutsideZeroToOneThousand_Throws(int permille)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.ApplyPermilleReduction(180L, permille));

        Assert.Equal("permille", exception.ParamName);
    }

    // ----- both helpers together, as the start rule uses them -----

    [Fact]
    public void CeilDiv_OfReducedLabor_GivesTheCycleSecondsOfTheStartRule()
    {
        // A sawmill cycle has 180 labor (core-instructions/23); research takes 250 permille off; 3 workers share the rest.
        long effectiveLabor = IntegerMath.ApplyPermilleReduction(180L, 250);

        Assert.Equal(135L, effectiveLabor);
        Assert.Equal(45L, IntegerMath.CeilDiv(effectiveLabor, 3L));
    }
}
