#nullable enable

using System.Globalization;
using System.Text;

namespace CityBuilder.Client.Presentation.Build
{
    /// <summary>
    /// Words a span of whole seconds for a card: <c>2m 30s</c>, <c>45s</c>, <c>1h</c>, <c>1h 5m 10s</c>. Only the units that are not zero are
    /// shown, the largest unit is the hour (a build or an upgrade never takes days in M0), and the text never depends on the machine's culture.
    /// </summary>
    public static class DurationText
    {
        /// <summary>Seconds in a minute and in an hour: units of time, not game numbers.</summary>
        private const long SecondsPerMinute = 60;

        /// <summary>Seconds in an hour.</summary>
        private const long SecondsPerHour = 3600;

        /// <summary>Formats <paramref name="seconds"/>; zero and a negative span (which no content has) are <c>0s</c>.</summary>
        public static string Format(long seconds)
        {
            if (seconds <= 0)
            {
                return "0s";
            }

            long hours = seconds / SecondsPerHour;
            long minutes = seconds % SecondsPerHour / SecondsPerMinute;
            long rest = seconds % SecondsPerMinute;
            var text = new StringBuilder();
            Append(text, hours, 'h');
            Append(text, minutes, 'm');
            Append(text, rest, 's');
            return text.ToString();
        }

        /// <summary>Adds <c>5m</c> to <paramref name="text"/> (after a space when it already has a part) unless the amount is zero.</summary>
        private static void Append(StringBuilder text, long amount, char unit)
        {
            if (amount == 0)
            {
                return;
            }

            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append(amount.ToString(CultureInfo.InvariantCulture)).Append(unit);
        }
    }
}
