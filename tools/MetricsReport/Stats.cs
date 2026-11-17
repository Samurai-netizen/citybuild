namespace CityBuilder.MetricsReport;

/// <summary>The integer arithmetic of the report (docs/plan/CR-0009-T2-PLAN.md §3.3): the lower median and rounding half up.</summary>
public static class Stats
{
    /// <summary>The lower middle value.</summary>
    /// <param name="values">The values, in any order.</param>
    /// <returns>The median, or <see langword="null"/> for no values.</returns>
    public static long? Median(IEnumerable<long> values)
    {
        long[] sorted = [.. values.Order()];
        return sorted.Length == 0 ? null : sorted[(sorted.Length - 1) / 2];
    }

    /// <summary>A quotient rounded to the nearest whole number, a half away from zero.</summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator, above zero.</param>
    /// <returns>The rounded quotient.</returns>
    public static long RoundedTenths(long numerator, long denominator)
    {
        long rounded = ((2 * Math.Abs(numerator)) + denominator) / (2 * denominator);
        return numerator < 0 ? -rounded : rounded;
    }
}
