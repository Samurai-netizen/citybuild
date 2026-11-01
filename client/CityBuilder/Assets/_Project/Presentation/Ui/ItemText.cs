#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Display;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// Words items for the screens, from the content's names and never the machine's culture: <c>150 Coins, 30 Logs</c> for a price and
    /// <c>60 Planks/h</c> for a rate. The build card and the building inspector share it, so one item reads the same on both. Pure code.
    /// </summary>
    public static class ItemText
    {
        /// <summary>Thousandths in a whole item: the engine's rates are thousandths of an item per hour (<see cref="ItemRate"/>).</summary>
        private const long MilliPerItem = 1000;

        /// <summary>The item's name from the content; the id when the content has none (it always has).</summary>
        public static string ItemName(GameContent content, ItemId item)
        {
            return content.TryGetItem(item, out ItemDefinition? definition) ? definition.Name : item.Value;
        }

        /// <summary><c>150 Coins, 30 Logs</c>: each amount and the content's name of its item, joined by <paramref name="separator"/>.</summary>
        public static string Quantities(GameContent content, IReadOnlyList<ItemQuantity> lines, string separator)
        {
            var text = new StringBuilder();
            for (int index = 0; index < lines.Count; index++)
            {
                text.Append(index == 0 ? string.Empty : separator)
                    .Append(lines[index].Quantity.ToString(CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(ItemName(content, lines[index].Item));
            }

            return text.ToString();
        }

        /// <summary>
        /// <c>60 Planks/h</c>, <c>2.5 Logs/h</c>: one rate, a part of an item shown up to three decimals without trailing zeros. Rates of
        /// several items are joined by a comma.
        /// </summary>
        /// <param name="content">The game content the names come from.</param>
        /// <param name="rates">The rates, in the order to show; the list may be empty (the result is then empty).</param>
        public static string Rates(GameContent content, IReadOnlyList<ItemRate> rates)
        {
            var text = new StringBuilder();
            for (int index = 0; index < rates.Count; index++)
            {
                long milli = rates[index].MilliPerHour;
                text.Append(index == 0 ? string.Empty : ", ").Append((milli / MilliPerItem).ToString(CultureInfo.InvariantCulture));
                long part = milli % MilliPerItem;
                if (part != 0)
                {
                    text.Append('.').Append(part.ToString("D3", CultureInfo.InvariantCulture).TrimEnd('0'));
                }

                text.Append(' ').Append(ItemName(content, rates[index].Item)).Append("/h");
            }

            return text.ToString();
        }
    }
}
