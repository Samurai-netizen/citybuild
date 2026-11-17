using System.Globalization;

namespace CityBuilder.MetricsReport;

/// <summary>
/// A percentage (or a difference in percentage points) as the page prints it (docs/plan/CR-0009-T2-PLAN.md §3.3): tenths computed from
/// whole counts, rounded half up (away from zero), none over a zero denominator, and marked when a denominator is under the least sample.
/// </summary>
/// <param name="Tenths">The value in tenths of a percent, or <see langword="null"/> when a denominator is zero.</param>
/// <param name="IsSmall">Whether a denominator holds fewer players than the least sample.</param>
public readonly record struct Rate(long? Tenths, bool IsSmall)
{
    /// <summary>What the page prints for a percentage over nobody.</summary>
    public const string None = "—";

    /// <summary>The share of a part in a whole.</summary>
    /// <param name="part">The part.</param>
    /// <param name="whole">The whole (players in the denominator).</param>
    /// <param name="minSample">The least sample.</param>
    /// <returns>The percentage.</returns>
    public static Rate Of(long part, long whole, int minSample) =>
        new(whole == 0 ? null : Stats.RoundedTenths(part * 1000, whole), whole < minSample);

    /// <summary>The difference of two shares in percentage points, from the four counts so the rounding happens once.</summary>
    /// <param name="part">The first share's part.</param>
    /// <param name="whole">The first share's whole.</param>
    /// <param name="otherPart">The second share's part.</param>
    /// <param name="otherWhole">The second share's whole.</param>
    /// <param name="minSample">The least sample.</param>
    /// <returns>The first share minus the second.</returns>
    public static Rate Difference(long part, long whole, long otherPart, long otherWhole, int minSample) =>
        new(
            whole == 0 || otherWhole == 0 ? null : Stats.RoundedTenths(((part * otherWhole) - (otherPart * whole)) * 1000, whole * otherWhole),
            whole < minSample || otherWhole < minSample);

    /// <summary>The value with one decimal (<c>12.5</c>, <c>-6.3</c>), or <see cref="None"/>.</summary>
    /// <returns>The printed value.</returns>
    public override string ToString()
    {
        if (Tenths is not long tenths)
        {
            return None;
        }

        long magnitude = Math.Abs(tenths);
        return string.Create(CultureInfo.InvariantCulture, $"{(tenths < 0 ? "-" : "")}{magnitude / 10}.{magnitude % 10}");
    }
}
