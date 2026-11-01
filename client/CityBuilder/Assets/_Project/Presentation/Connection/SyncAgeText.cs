#nullable enable

using System.Globalization;

namespace CityBuilder.Client.Presentation.Connection
{
    /// <summary>
    /// Words how long ago the client last got a snapshot from the server (M0-55): <c>just now</c>, <c>45s ago</c>, <c>3m ago</c>, <c>2h 5m ago</c>. The
    /// unit is coarse on purpose: a banner that says <c>3m 20s</c> would change every second for no reason a player cares about. The text never
    /// depends on the machine's culture.
    /// </summary>
    public static class SyncAgeText
    {
        /// <summary>Seconds in a minute and in an hour: units of time, not game numbers.</summary>
        private const long SecondsPerMinute = 60;

        /// <summary>Seconds in an hour.</summary>
        private const long SecondsPerHour = 3600;

        /// <summary>Under this many seconds the age is <c>just now</c>: a poll or a command answer is that fresh.</summary>
        private const long JustNowSeconds = 5;

        /// <summary>The age of a snapshot received <paramref name="seconds"/> ago; a negative span counts as zero.</summary>
        public static string Format(long seconds)
        {
            if (seconds < JustNowSeconds)
            {
                return "just now";
            }

            if (seconds < SecondsPerMinute)
            {
                return Number(seconds) + "s ago";
            }

            if (seconds < SecondsPerHour)
            {
                return Number(seconds / SecondsPerMinute) + "m ago";
            }

            long hours = seconds / SecondsPerHour;
            long minutes = seconds % SecondsPerHour / SecondsPerMinute;
            return Number(hours) + "h" + (minutes == 0 ? string.Empty : " " + Number(minutes) + "m") + " ago";
        }

        /// <summary>Plain digits.</summary>
        private static string Number(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
