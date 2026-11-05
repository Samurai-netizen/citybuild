#nullable enable

using System.Collections.Generic;
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

        /// <summary>
        /// A good shows its name and the city's total; there is no city-wide cap since CR-0008 (limits are per store), so the cap text is empty
        /// and the chip is not full. With no producers the rate is zero.
        /// </summary>
        [Test]
        public void Good_ShowsItsCityTotal_AndNoCap()
        {
            ItemChipModel chip = Chip("logs", HudTestData.City(0, 700, 40, 10, new Building[0]));

            Assert.That(chip.Name, Is.EqualTo("Logs"));
            Assert.That(chip.StockText, Is.EqualTo("40"));
            Assert.That(chip.CapText, Is.Empty);
            Assert.That(chip.IsFull, Is.False);
            Assert.That(chip.RateText, Is.EqualTo("0/h"));
            Assert.That(chip.Trend, Is.EqualTo(RateTrend.Flat));
        }

        /// <summary>Coins come from the purse: no cap, never full.</summary>
        [Test]
        public void Coins_ShowThePurse()
        {
            ItemChipModel chip = Chip("coins", HudTestData.City(0, 700, 40, 10, new Building[0]));

            Assert.That(chip.StockText, Is.EqualTo("700"));
            Assert.That(chip.CapText, Is.Empty);
            Assert.That(chip.IsFull, Is.False);
        }

        /// <summary>
        /// The total counts every store and every load on the way (CR-0008): 30 logs in a woodcutter, 40 in the warehouse and 5 carried make 75.
        /// A store at its limit does not make the city's chip full: the stall shows on the building.
        /// </summary>
        [Test]
        public void Total_CountsEveryStoreAndEveryLoad()
        {
            var logs = new ItemId("logs");
            var woodcutter = new Building(
                1, new BuildingTypeId("woodcutter"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0,
                new[] { new KeyValuePair<ItemId, long>(logs, 30) });
            var warehouse = new Building(
                2, new BuildingTypeId("warehouse"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0,
                new[] { new KeyValuePair<ItemId, long>(logs, 40) });
            var city = new CityState(
                10, 1, 3, 0, new[] { woodcutter, warehouse }, new ResearchId[0], null, null, new[] { new Trip(1, 1, 2, logs, 5, 8, 20) }, 2);

            ItemChipModel chip = Chip("logs", city);

            Assert.That(chip.StockText, Is.EqualTo("75"));
            Assert.That(chip.IsFull, Is.False);
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
