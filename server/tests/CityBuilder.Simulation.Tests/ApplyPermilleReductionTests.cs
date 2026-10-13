using System.Numerics;

namespace CityBuilder.Simulation.Tests;

/// <summary>Edge cases of the labor reduction that research effects apply to every recipe (permille, rounded up).</summary>
public sealed class ApplyPermilleReductionTests
{
    [Fact]
    public void PermilleScale_Value_IsOneThousand()
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
    [InlineData(180L, 500, 90L)] // 500 permille is the cap on the total reduction per building type (MaxLaborReductionPermille in 23)
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

    // The bad argument is paired with several partners (edge values, and the other argument bad as well), so that no shortcut ahead of
    // the guards can let bad input through. When both are out of range the labor is reported: it is checked first.
    [Theory]
    [InlineData(-1L, 0)]
    [InlineData(-1L, 500)]
    [InlineData(-1L, 1_000)]
    [InlineData(long.MinValue, 0)]
    [InlineData(long.MinValue, 1_000)]
    [InlineData(-1L, -1)]
    [InlineData(-1L, 1_001)]
    [InlineData(long.MinValue, int.MaxValue)]
    public void ApplyPermilleReduction_NegativeLabor_Throws(long labor, int permille)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.ApplyPermilleReduction(labor, permille));

        Assert.Equal("labor", exception.ParamName);
    }

    [Theory]
    [InlineData(180L, -1)]
    [InlineData(180L, 1_001)]
    [InlineData(0L, -1)]
    [InlineData(0L, 1_001)]
    [InlineData(long.MaxValue, -1)]
    [InlineData(long.MaxValue, 1_001)]
    [InlineData(0L, int.MinValue)]
    [InlineData(long.MaxValue, int.MaxValue)]
    public void ApplyPermilleReduction_PermilleOutsideZeroToOneThousand_Throws(long labor, int permille)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.ApplyPermilleReduction(labor, permille));

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
