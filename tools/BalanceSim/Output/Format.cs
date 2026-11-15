using System.Globalization;

namespace CityBuilder.BalanceSim.Output;

/// <summary>How the output files write numbers: integers only, the invariant culture, so every machine writes the same bytes.</summary>
public static class Format
{
    /// <summary>A clock second as <c>H:MM:SS</c> (hours not wrapped at 24), for example <c>27:05:00</c>.</summary>
    /// <param name="seconds">The seconds, at least 0.</param>
    /// <returns>The text.</returns>
    public static string Clock(long seconds) => string.Create(
        CultureInfo.InvariantCulture, $"{seconds / 3_600}:{seconds % 3_600 / 60:00}:{seconds % 60:00}");

    /// <summary>A clock second as <see cref="Clock(long)"/>, or a dash for "never".</summary>
    /// <param name="seconds">The seconds, or null.</param>
    /// <returns>The text.</returns>
    public static string Clock(long? seconds) => seconds.HasValue ? Clock(seconds.Value) : "—";

    /// <summary>An integer in the invariant culture.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A share as a percentage with one decimal, rounded down: <c>12.3 %</c>; <c>—</c> when the whole is 0.</summary>
    /// <param name="part">The part.</param>
    /// <param name="whole">The whole.</param>
    /// <returns>The text.</returns>
    public static string Percent(long part, long whole)
    {
        if (whole <= 0)
        {
            return "—";
        }

        long permille = checked(part * 1_000) / whole;
        return string.Create(CultureInfo.InvariantCulture, $"{permille / 10}.{permille % 10} %");
    }

    /// <summary>A rate per hour, rounded down: <paramref name="quantity"/> × 3600 / <paramref name="seconds"/>; 0 when no time passed.</summary>
    /// <param name="quantity">The quantity.</param>
    /// <param name="seconds">The seconds it took.</param>
    /// <returns>The rate.</returns>
    public static long PerHour(long quantity, long seconds) => seconds <= 0 ? 0 : checked(quantity * 3_600) / seconds;

    /// <summary>An average with one decimal, rounded down: <c>1.5</c>; <c>—</c> when the count is 0.</summary>
    /// <param name="sum">The sum.</param>
    /// <param name="count">What it is divided by.</param>
    /// <returns>The text.</returns>
    public static string Average(long sum, long count)
    {
        if (count <= 0)
        {
            return "—";
        }

        long tenths = checked(sum * 10) / count;
        return string.Create(CultureInfo.InvariantCulture, $"{tenths / 10}.{tenths % 10}");
    }
}
