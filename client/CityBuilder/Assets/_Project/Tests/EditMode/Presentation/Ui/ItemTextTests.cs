#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Display;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Ui
{
    /// <summary>The words for items and rates that the build card and the building inspector share (M0-51).</summary>
    public sealed class ItemTextTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>A rate shows whole items with the item's name, a part of an item with up to three decimals, and an empty list shows nothing.</summary>
        [Test]
        public void Rates_ShowWholeItemsAndDecimals()
        {
            var rates = new List<ItemRate>
            {
                new ItemRate(new ItemId("planks"), 60000),
                new ItemRate(new ItemId("logs"), 2500),
                new ItemRate(new ItemId("coins"), 125),
                new ItemRate(new ItemId("planks"), 0),
            };

            Assert.That(ItemText.Rates(_content, rates), Is.EqualTo("60 Planks/h, 2.5 Logs/h, 0.125 Coins/h, 0 Planks/h"));
            Assert.That(ItemText.Rates(_content, new List<ItemRate>()), Is.Empty);
        }

        /// <summary>An item the content does not name shows by its id.</summary>
        [Test]
        public void ItemName_UnknownItem_IsItsId()
        {
            Assert.That(ItemText.ItemName(_content, new ItemId("planks")), Is.EqualTo("Planks"));
            Assert.That(ItemText.ItemName(_content, new ItemId("gold")), Is.EqualTo("gold"));
        }
    }
}
