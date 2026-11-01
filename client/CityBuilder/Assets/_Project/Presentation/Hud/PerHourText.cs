#nullable enable

using System.Globalization;

namespace CityBuilder.Client.Presentation.Hud
{
    /// <summary>
    /// Words a net rate for a chip: <c>+60/h</c>, <c>−30/h</c> (a real minus sign, U+2212), <c>0/h</c>. The engine's rate is in thousandths of an
    /// item per hour (<c>ItemRate</c>); the text shows whole items, or one tenth when the rate is not whole (<c>+12.5/h</c>), rounded toward zero as every
    /// display estimate is (<c>04</c> §15). A rate below a tenth reads <c>0/h</c>. Integer arithmetic only, and never the machine's culture.
    /// </summary>
    public static class PerHourText
    {
        /// <summary>Thousandths in one tenth of an item: a unit of the engine's rate, not a game number.</summary>
        private const long MilliPerTenth = 100;

        /// <summary>Tenths in one item.</summary>
        private const long TenthsPerItem = 10;

        /// <summary>The text of a rate of <paramref name="milliPerHour"/> thousandths of an item per hour.</summary>
        public static string Format(long milliPerHour)
        {
            long tenths = Tenths(milliPerHour);
            if (tenths == 0)
            {
                return "0/h";
            }

            long whole = tenths / TenthsPerItem;
            long tenth = tenths % TenthsPerItem;
            string number = whole.ToString(CultureInfo.InvariantCulture);
            if (tenth != 0)
            {
                number += "." + tenth.ToString(CultureInfo.InvariantCulture);
            }

            return (milliPerHour < 0 ? "−" : "+") + number + "/h";
        }

        /// <summary>Which way the rate points as <see cref="Format"/> shows it: a rate that shows as <c>0/h</c> is <see cref="RateTrend.Flat"/>.</summary>
        public static RateTrend TrendOf(long milliPerHour)
        {
            if (Tenths(milliPerHour) == 0)
            {
                return RateTrend.Flat;
            }

            return milliPerHour < 0 ? RateTrend.Falling : RateTrend.Rising;
        }

        /// <summary>The size of the rate in whole tenths of an item per hour, rounded toward zero; the sign is left to the caller.</summary>
        private static long Tenths(long milliPerHour)
        {
            long size = milliPerHour < 0 ? -milliPerHour : milliPerHour;
            return size / MilliPerTenth;
        }
    }
}
