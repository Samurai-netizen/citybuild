#nullable enable

using System;
using System.Globalization;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Hud
{
    /// <summary>
    /// One item chip of the HUD as plain text and flags: the item's name, the city's total of it (every store, every load, the purse for coins;
    /// CR-0008) and its net rate per hour. Every number comes from the displayed city and the engine's helpers (<c>CityQueries.Total</c>,
    /// <c>ProductionDisplay.NetPerHour</c>); none is written here. Limits are per store now, so a city-wide cap is no more: the cap text stays
    /// empty and the chip is never full (CR-0008-T2 shows the stores). It is immutable, and two chips that say the same compare equal, so the view redraws only what changed.
    /// </summary>
    public sealed class ItemChipModel : IEquatable<ItemChipModel>
    {
        /// <summary>Creates a chip; use <see cref="Create"/>.</summary>
        private ItemChipModel(ItemId item, string name, string stockText, string capText, bool isFull, string rateText, RateTrend trend)
        {
            Item = item;
            Name = name;
            StockText = stockText;
            CapText = capText;
            IsFull = isFull;
            RateText = rateText;
            Trend = trend;
        }

        /// <summary>The item the chip shows.</summary>
        public ItemId Item { get; }

        /// <summary>The item's name from the content, such as <c>Logs</c>; it stands in for an icon until there is art.</summary>
        public string Name { get; }

        /// <summary>The city's total in the displayed city, such as <c>40</c>.</summary>
        public string StockText { get; }

        /// <summary>A city-wide cap; always empty since CR-0008 (limits are per store), kept for the view until CR-0008-T2.</summary>
        public string CapText { get; }

        /// <summary>Whether a city-wide cap is reached; always false since CR-0008 (limits are per store), kept for the view until CR-0008-T2.</summary>
        public bool IsFull { get; }

        /// <summary>The net rate per hour, such as <c>+60/h</c>, <c>−30/h</c> or <c>0/h</c> (<see cref="PerHourText"/>).</summary>
        public string RateText { get; }

        /// <summary>Which way the rate points, for the colour of <see cref="RateText"/>.</summary>
        public RateTrend Trend { get; }

        /// <summary>Makes the chip of <paramref name="definition"/> for <paramref name="city"/>, the city the client displays now.</summary>
        /// <param name="content">The game content the rates and caps come from.</param>
        /// <param name="city">The displayed city.</param>
        /// <param name="definition">The item.</param>
        public static ItemChipModel Create(GameContent content, CityState city, ItemDefinition definition)
        {
            long stock = CityQueries.Total(city, content, definition.Id);
            string capText = string.Empty;
            bool isFull = false;

            long net = ProductionDisplay.NetPerHour(city, content, definition.Id);
            return new ItemChipModel(
                definition.Id,
                definition.Name,
                stock.ToString(CultureInfo.InvariantCulture),
                capText,
                isFull,
                PerHourText.Format(net),
                PerHourText.TrendOf(net));
        }

        /// <summary>True when every text and flag is the same: the view has nothing to redraw.</summary>
        public bool Equals(ItemChipModel? other)
        {
            return other != null
                && Item.Equals(other.Item)
                && Name == other.Name
                && StockText == other.StockText
                && CapText == other.CapText
                && IsFull == other.IsFull
                && RateText == other.RateText
                && Trend == other.Trend;
        }

        /// <summary>See <see cref="Equals(ItemChipModel?)"/>.</summary>
        public override bool Equals(object? obj) => Equals(obj as ItemChipModel);

        /// <summary>A hash of the item and the texts that go with <see cref="Equals(ItemChipModel?)"/>.</summary>
        public override int GetHashCode() => Item.GetHashCode() ^ StockText.GetHashCode() ^ RateText.GetHashCode();
    }
}
