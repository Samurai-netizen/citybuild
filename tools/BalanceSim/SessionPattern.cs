using System.Globalization;

namespace CityBuilder.BalanceSim;

/// <summary>
/// When the simulated player checks in (docs/how-to/run-balance-simulator.md → Check-in patterns). Three forms:
/// <c>08:00,13:00,20:00 daily</c> (times of day), <c>every 4h</c> or <c>every 90min</c> (a fixed interval) and <c>once a day</c> (the same as
/// <c>08:00 daily</c>). The run starts at the first check-in: city clock 0 is that moment.
/// </summary>
public sealed class SessionPattern
{
    /// <summary>The seconds in a day.</summary>
    private const long _day = 86_400;

    /// <summary>The check-in times as seconds after midnight, ascending, for a daily pattern; empty for an interval pattern.</summary>
    private readonly long[] _timesOfDay;

    /// <summary>The seconds between check-ins for an interval pattern; 0 for a daily pattern.</summary>
    private readonly long _interval;

    /// <summary>Creates a pattern; only <see cref="Parse"/> does.</summary>
    /// <param name="text">The pattern as written.</param>
    /// <param name="slug">The folder-safe name.</param>
    /// <param name="timesOfDay">The times of day in seconds, ascending, or empty.</param>
    /// <param name="interval">The interval in seconds, or 0.</param>
    private SessionPattern(string text, string slug, long[] timesOfDay, long interval)
    {
        Text = text;
        Slug = slug;
        _timesOfDay = timesOfDay;
        _interval = interval;
    }

    /// <summary>The pattern as the user wrote it, trimmed.</summary>
    public string Text { get; }

    /// <summary>A name for folders and tables: <c>daily-0800-1300-2000</c>, <c>every-4h</c>, <c>every-90min</c>.</summary>
    public string Slug { get; }

    /// <summary>Reads a pattern.</summary>
    /// <param name="text">The text, for example <c>08:00,13:00,20:00 daily</c>, <c>every 4h</c>, <c>every 4 h</c> or <c>once a day</c>.</param>
    /// <returns>The pattern.</returns>
    /// <exception cref="FormatException">The text is none of the three forms, a time is not <c>HH:MM</c> below 24:00, or an interval is not above 0.</exception>
    public static SessionPattern Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string trimmed = text.Trim();
        string lower = trimmed.ToLowerInvariant();
        if (lower == "once a day")
        {
            return Daily(trimmed, ["08:00"]);
        }

        if (lower.EndsWith(" daily", StringComparison.Ordinal))
        {
            string[] times = lower[..^" daily".Length].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return Daily(trimmed, times);
        }

        if (lower.StartsWith("every ", StringComparison.Ordinal))
        {
            string amount = lower["every ".Length..].Replace(" ", string.Empty, StringComparison.Ordinal);
            (string unit, long seconds) = amount.EndsWith("min", StringComparison.Ordinal) ? ("min", 60L)
                : amount.EndsWith('h') ? ("h", 3_600L)
                : throw new FormatException($"The interval in \"{trimmed}\" needs a unit: h or min (for example \"every 4h\").");
            string number = amount[..^unit.Length];
            if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out long count) || count < 1)
            {
                throw new FormatException($"The interval in \"{trimmed}\" must be a whole number above 0.");
            }

            return new SessionPattern(trimmed, $"every-{count}{unit}", [], checked(count * seconds));
        }

        throw new FormatException(
            $"\"{trimmed}\" is not a check-in pattern. Use \"08:00,13:00,20:00 daily\", \"every 4h\", \"every 90min\" or \"once a day\".");
    }

    /// <summary>The check-in start times on the city clock, ascending, below <paramref name="runSeconds"/>; the first is always 0.</summary>
    /// <param name="runSeconds">The length of the run.</param>
    /// <returns>The start times.</returns>
    public IReadOnlyList<long> Starts(long runSeconds)
    {
        var starts = new List<long>();
        if (_interval > 0)
        {
            for (long time = 0; time < runSeconds; time += _interval)
            {
                starts.Add(time);
            }

            return starts;
        }

        long first = _timesOfDay[0];
        for (long day = 0; ; day++)
        {
            foreach (long timeOfDay in _timesOfDay)
            {
                long time = (day * _day) + timeOfDay - first;
                if (time >= runSeconds)
                {
                    return starts;
                }

                starts.Add(time);
            }
        }
    }

    /// <summary>Makes a daily pattern from its <c>HH:MM</c> times.</summary>
    /// <param name="text">The pattern as written.</param>
    /// <param name="times">The times.</param>
    /// <returns>The pattern.</returns>
    /// <exception cref="FormatException">There is no time, or one is not <c>HH:MM</c> below 24:00.</exception>
    private static SessionPattern Daily(string text, string[] times)
    {
        if (times.Length == 0)
        {
            throw new FormatException($"\"{text}\" names no time of day.");
        }

        long[] seconds = times.Select(time => ParseTime(text, time)).Distinct().Order().ToArray();
        string slug = "daily-" + string.Join('-', seconds.Select(value => (value / 3_600).ToString("00", CultureInfo.InvariantCulture)
            + (value % 3_600 / 60).ToString("00", CultureInfo.InvariantCulture)));
        return new SessionPattern(text, slug, seconds, 0);
    }

    /// <summary>Reads one <c>HH:MM</c> time of day.</summary>
    /// <param name="text">The whole pattern, for the message.</param>
    /// <param name="time">The time.</param>
    /// <returns>Seconds after midnight.</returns>
    /// <exception cref="FormatException">The time is not <c>HH:MM</c> below 24:00.</exception>
    private static long ParseTime(string text, string time)
    {
        string[] parts = time.Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int hours)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes)
            && hours < 24 && minutes < 60 && parts[1].Length == 2)
        {
            return (hours * 3_600L) + (minutes * 60L);
        }

        throw new FormatException($"\"{time}\" in \"{text}\" is not a time of day (HH:MM, below 24:00).");
    }
}
