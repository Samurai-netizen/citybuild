using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace CityBuilder.MetricsReport;

/// <summary>
/// Writes the report page's HTML (docs/plan/CR-0009-T2-PLAN.md §3.4): every text from the data or the checkpoint file goes through
/// <see cref="Text"/> (HTML-encoded); table cells carry a <c>data-sort</c> key for the page's sort script; numbers are written with
/// the invariant culture and lines end with <c>\n</c>, so the same input gives the same bytes on every machine.
/// </summary>
internal sealed class HtmlBuilder
{
    /// <summary>The encoder of texts: it escapes markup and leaves letters of every script readable.</summary>
    private static readonly HtmlEncoder _encoder = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>The page so far.</summary>
    private readonly StringBuilder _page = new();

    /// <summary>Makes a builder for a page whose small-sample mark names the least sample.</summary>
    /// <param name="minSample">The least sample, for the mark's tooltip.</param>
    public HtmlBuilder(int minSample) => MinSample = minSample;

    /// <summary>The least sample, for the mark's tooltip.</summary>
    public int MinSample { get; }

    /// <summary>Appends markup as it is; never pass text from the data here.</summary>
    /// <param name="markup">The markup.</param>
    /// <returns>This builder.</returns>
    public HtmlBuilder Raw(string markup)
    {
        _page.Append(markup);
        return this;
    }

    /// <summary>Appends markup and a line end.</summary>
    /// <param name="markup">The markup.</param>
    /// <returns>This builder.</returns>
    public HtmlBuilder Line(string markup = "")
    {
        _page.Append(markup).Append('\n');
        return this;
    }

    /// <summary>Appends a text, HTML-encoded.</summary>
    /// <param name="text">The text.</param>
    /// <returns>This builder.</returns>
    public HtmlBuilder Text(string text)
    {
        _page.Append(Encode(text));
        return this;
    }

    /// <summary>HTML-encodes a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The encoded text.</returns>
    public static string Encode(string text) => _encoder.Encode(text);

    /// <summary>Opens a sortable table and writes its header row.</summary>
    /// <param name="id">The table's id, or none.</param>
    /// <param name="headers">Per column its title and whether it sorts as a number.</param>
    /// <returns>This builder.</returns>
    public HtmlBuilder TableStart(string? id, params (string Title, bool Numeric)[] headers)
    {
        Raw(id is null ? "<table class=\"sortable\">" : $"<table class=\"sortable\" id=\"{id}\">").Line("<thead><tr>");
        foreach ((string title, bool numeric) in headers)
        {
            Raw($"<th data-type=\"{(numeric ? "num" : "text")}\"{(numeric ? " class=\"n\"" : "")}>").Text(title).Line("</th>");
        }

        return Line("</tr></thead>").Line("<tbody>");
    }

    /// <summary>Closes a table opened by <see cref="TableStart"/>.</summary>
    /// <returns>This builder.</returns>
    public HtmlBuilder TableEnd() => Line("</tbody></table>");

    /// <summary>A text cell that sorts by its text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>This builder.</returns>
    public HtmlBuilder TextCell(string text) => Raw($"<td data-sort=\"{Encode(text)}\">").Text(text).Line("</td>");

    /// <summary>A whole-number cell.</summary>
    /// <param name="value">The number.</param>
    /// <returns>This builder.</returns>
    public HtmlBuilder NumberCell(long value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        return Line($"<td class=\"n\" data-sort=\"{text}\">{text}</td>");
    }

    /// <summary>A cell of a number that may be missing (a median over nobody): <c>—</c>, sorted below every number.</summary>
    /// <param name="value">The number, or none.</param>
    /// <returns>This builder.</returns>
    public HtmlBuilder NumberCell(long? value) =>
        value is long number ? NumberCell(number) : Line($"<td class=\"n\" data-sort=\"\">{Rate.None}</td>");

    /// <summary>A duration cell (seconds, shown as days, hours, minutes or seconds).</summary>
    /// <param name="seconds">The seconds, or none.</param>
    /// <returns>This builder.</returns>
    public HtmlBuilder DurationCell(long? seconds) =>
        Line($"<td class=\"n\" data-sort=\"{Key(seconds)}\">{Duration(seconds)}</td>");

    /// <summary>A percentage cell, with the small-sample mark and, when asked, a bar.</summary>
    /// <param name="rate">The percentage.</param>
    /// <param name="unit">The unit after the number (<c>%</c>, or <c>pp</c> for a difference).</param>
    /// <param name="bar">Whether to draw a bar under it.</param>
    /// <returns>This builder.</returns>
    public HtmlBuilder RateCell(Rate rate, string unit = "%", bool bar = false)
    {
        Raw($"<td class=\"n\" data-sort=\"{Key(rate.Tenths)}\">").Raw(RateText(rate, unit));
        if (bar && rate.Tenths is long tenths)
        {
            string width = string.Create(CultureInfo.InvariantCulture, $"{Math.Clamp(tenths, 0, 1000) / 10}.{Math.Clamp(tenths, 0, 1000) % 10}");
            Raw($"<span class=\"bar\"><span style=\"width:{width}%\"></span></span>");
        }

        return Line("</td>");
    }

    /// <summary>A percentage as text: the number and its unit, or <c>—</c>, and the small-sample mark.</summary>
    /// <param name="rate">The percentage.</param>
    /// <param name="unit">The unit.</param>
    /// <returns>The markup.</returns>
    public string RateText(Rate rate, string unit = "%") =>
        (rate.Tenths is null ? Rate.None : $"{rate} {unit}")
        + (rate.IsSmall ? $" <span class=\"small\" title=\"small sample: fewer than {MinSample} players\">*</span>" : "");

    /// <summary>A sort key of a number that may be missing: its digits, or empty (the script sorts empty below every number).</summary>
    /// <param name="value">The number.</param>
    /// <returns>The key.</returns>
    public static string Key(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    /// <summary>A duration as a person reads it: <c>45 s</c>, <c>12 min</c>, <c>3 h 5 min</c>, <c>2 d 4 h</c>; <c>—</c> for none.</summary>
    /// <param name="seconds">The seconds, or none.</param>
    /// <returns>The text.</returns>
    public static string Duration(long? seconds) => seconds switch
    {
        null => Rate.None,
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{seconds} s"),
        < 3_600 => string.Create(CultureInfo.InvariantCulture, $"{seconds / 60} min"),
        < 86_400 => string.Create(CultureInfo.InvariantCulture, $"{seconds / 3_600} h {seconds % 3_600 / 60} min"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{seconds / 86_400} d {seconds % 86_400 / 3_600} h"),
    };

    /// <summary>The page.</summary>
    /// <returns>The HTML.</returns>
    public override string ToString() => _page.ToString();
}
