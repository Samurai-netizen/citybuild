using System.Globalization;
using CityBuilder.Simulation.Content;

namespace CityBuilder.DocTools.Pages;

/// <summary>How the pages write numbers, durations and lists of goods: invariant culture, so every machine writes the same text.</summary>
public static class PageFormat
{
    /// <summary>An integer in the invariant culture, with no group separators (the content's own spelling).</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text, for example <c>3600</c>.</returns>
    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Thousandths as a decimal number with at most three decimals and no trailing zeros, for example 7500 → <c>7.5</c>, 333 → <c>0.333</c>.
    /// The engine's display rates are thousandths of an item per hour, rounded down (<c>ProductionDisplay</c>).
    /// </summary>
    /// <param name="milli">The value in thousandths, 0 or more.</param>
    /// <returns>The text.</returns>
    public static string Milli(long milli)
    {
        string whole = Number(milli / 1000);
        long fraction = milli % 1000;
        return fraction == 0 ? whole : whole + "." + fraction.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');
    }

    /// <summary>A duration in seconds, with a readable form after it from one minute up, for example <c>5400 s (1 h 30 min)</c>.</summary>
    /// <param name="seconds">The duration, 0 or more.</param>
    /// <returns>The text.</returns>
    public static string Duration(long seconds)
    {
        string exact = Number(seconds) + " s";
        if (seconds < 60)
        {
            return exact;
        }

        var parts = new List<string>();
        (long days, long rest) = Math.DivRem(seconds, 86_400);
        (long hours, rest) = Math.DivRem(rest, 3600);
        (long minutes, long secondsLeft) = Math.DivRem(rest, 60);
        AddPart(parts, days, "d");
        AddPart(parts, hours, "h");
        AddPart(parts, minutes, "min");
        AddPart(parts, secondsLeft, "s");
        return exact + " (" + string.Join(" ", parts) + ")";
    }

    /// <summary>Goods and amounts as <c>80 coins, 20 planks</c> with the ids in code, in the content's order (sorted by item id), or a dash for none.</summary>
    /// <param name="quantities">The goods.</param>
    /// <returns>The text.</returns>
    public static string Goods(IReadOnlyList<ItemQuantity> quantities) =>
        quantities.Count == 0
            ? "—"
            : string.Join(", ", quantities.Select(quantity => Number(quantity.Quantity) + " " + MarkdownPage.Code(quantity.Item.Value)));

    /// <summary>Ids as a comma-separated list of inline code, or a dash for none.</summary>
    /// <param name="ids">The ids, in the order to show them.</param>
    /// <returns>The text.</returns>
    public static string Codes(IEnumerable<string> ids)
    {
        List<string> all = [.. ids];
        return all.Count == 0 ? "—" : string.Join(", ", all.Select(MarkdownPage.Code));
    }

    /// <summary>Adds "n unit" to a duration's parts when n is above 0.</summary>
    /// <param name="parts">The parts so far.</param>
    /// <param name="amount">How many of the unit.</param>
    /// <param name="unit">The unit's short name.</param>
    private static void AddPart(List<string> parts, long amount, string unit)
    {
        if (amount > 0)
        {
            parts.Add(Number(amount) + " " + unit);
        }
    }
}
