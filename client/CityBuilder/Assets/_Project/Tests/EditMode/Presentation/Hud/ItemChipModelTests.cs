#nullable enable

using CityBuilder.Client.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Hud
{
    /// <summary>The words of one item chip (M0-49): name, stock, cap, the full flag and the net rate from the engine's helpers.</summary>
    public sealed class ItemChipModelTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>The chip of <paramref name="itemId"/> for <paramref name="city"/>.</summary>
        private ItemChipModel Chip(string itemId, CityState city)
        {
            Assert.That(_content.TryGetItem(new ItemId(itemId), out ItemDefinition? definition), Is.True);
            return ItemChipModel.Create(_content, city, definition!);
        }

        /// <summary>A capped item shows its name, stock and cap; with no buildings the rate is zero.</summary>
        [Test]
        public void CappedItem_ShowsStockAndCap()
        {
            ItemChipModel chip = Chip("logs", HudTestData.City(0, 700, 40, 10, new Building[0]));

            Assert.That(chip.Name, Is.EqualTo("Logs"));
            Assert.That(chip.StockText, Is.EqualTo("40"));
            Assert.That(chip.CapText, Is.EqualTo("500"));
            Assert.That(chip.IsFull, Is.False);
            Assert.That(chip.RateText, Is.EqualTo("0/h"));
            Assert.That(chip.Trend, Is.EqualTo(RateTrend.Flat));
        }

        /// <summary>Coins have no cap: the cap text is empty and the chip is never full.</summary>
        [Test]
        public void UncappedItem_HasNoCap()
        {
            ItemChipModel chip = Chip("coins", HudTestData.City(0, 700, 40, 10, new Building[0]));

            Assert.That(chip.StockText, Is.EqualTo("700"));
            Assert.That(chip.CapText, Is.Empty);
            Assert.That(chip.IsFull, Is.False);
        }

        /// <summary>A stock at the cap is full.</summary>
        [Test]
        public void StockAtTheCap_IsFull()
        {
            Assert.That(Chip("logs", HudTestData.City(0, 0, 500, 0, new Building[0])).IsFull, Is.True);
            Assert.That(Chip("logs", HudTestData.City(0, 0, 499, 0, new Building[0])).IsFull, Is.False);
        }

        /// <summary>A completed storage research raises the cap the chip shows.</summary>
        [Test]
        public void StorageResearch_RaisesTheShownCap()
        {
            CityState city = HudTestData.City(0, 0, 40, 0, new Building[0], new[] { "trade_charter", "storehouses" });

            Assert.That(Chip("logs", city).CapText, Is.EqualTo("750"));
        }

        /// <summary>A woodcutter with two workers makes a log every 30 s: +120 an hour.</summary>
        [Test]
        public void Producer_ShowsAGain()
        {
            CityState city = HudTestData.City(0, 0, 0, 0, new[] { HudTestData.Producer(1, 0, "woodcutter", 2, 60) });

            ItemChipModel chip = Chip("logs", city);

            Assert.That(chip.RateText, Is.EqualTo("+120/h"));
            Assert.That(chip.Trend, Is.EqualTo(RateTrend.Rising));
        }

        /// <summary>A sawmill alone uses more logs than it makes: the logs chip shows a loss, the planks chip a gain.</summary>
        [Test]
        public void Consumer_ShowsALossOnItsInput_AndAGainOnItsOutput()
        {
            CityState city = HudTestData.City(0, 0, 100, 0, new[] { HudTestData.Producer(1, 0, "sawmill", 3, 180) });

            ItemChipModel logs = Chip("logs", city);
            ItemChipModel planks = Chip("planks", city);

            Assert.That(logs.RateText, Is.EqualTo("−120/h"));
            Assert.That(logs.Trend, Is.EqualTo(RateTrend.Falling));
            Assert.That(planks.RateText, Is.EqualTo("+60/h"));
        }

        /// <summary>A woodcutter feeding a sawmill cancels out on the logs: the rate is zero.</summary>
        [Test]
        public void FeedingASawmill_CancelsOut()
        {
            CityState city = HudTestData.City(
                0,
                0,
                100,
                0,
                new[] { HudTestData.Producer(1, 0, "woodcutter", 2, 60), HudTestData.Producer(2, 1, "sawmill", 3, 180) });

            Assert.That(Chip("logs", city).RateText, Is.EqualTo("0/h"));
        }

        /// <summary>Two chips that say the same are equal; a different stock makes them differ.</summary>
        [Test]
        public void Equality_FollowsTheTexts()
        {
            CityState a = HudTestData.City(0, 0, 40, 0, new Building[0]);
            CityState b = HudTestData.City(0, 0, 40, 0, new Building[0]);
            CityState c = HudTestData.City(0, 0, 41, 0, new Building[0]);

            Assert.That(Chip("logs", a), Is.EqualTo(Chip("logs", b)));
            Assert.That(Chip("logs", a), Is.Not.EqualTo(Chip("logs", c)));
        }
    }
}
