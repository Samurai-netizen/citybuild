#nullable enable

namespace CityBuilder.Simulation.Display
{
    /// <summary>
    /// How fast an item is made or used, for display only (<see cref="ProductionDisplay"/>). The rate is in thousandths of an item per hour, so
    /// a producer slower than one item an hour still shows a rate, and a client can print one decimal (12500 is "12.5 per hour") without any
    /// floating point. It is never used to decide anything: the engine's cycles are the authority.
    /// </summary>
    public sealed class ItemRate
    {
        /// <summary>Creates a rate.</summary>
        /// <param name="item">The item.</param>
        /// <param name="milliPerHour">Thousandths of an item per hour; negative only for a net rate that uses more than it makes.</param>
        public ItemRate(ItemId item, long milliPerHour)
        {
            Item = item;
            MilliPerHour = milliPerHour;
        }

        /// <summary>The item.</summary>
        public ItemId Item { get; }

        /// <summary>Thousandths of an item per hour, rounded down.</summary>
        public long MilliPerHour { get; }

        /// <summary>The rate as text, for logs and test messages: <c>logs:12500</c>.</summary>
        /// <returns>The item, a colon and <see cref="MilliPerHour"/>.</returns>
        public override string ToString() => Item.Value + ":" + MilliPerHour.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
