#nullable enable

using System;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Build
{
    /// <summary>The words of one build card (M0-45): its texts from the content, the lock, and the hints from a city.</summary>
    public sealed class BuildCardModelTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = BuildMenuTestData.Content();
        }

        /// <summary>The card of <paramref name="typeId"/> for <paramref name="city"/>.</summary>
        private BuildCardModel Card(string typeId, CityState city)
        {
            Assert.That(_content.TryGetBuilding(new BuildingTypeId(typeId), out BuildingDefinition? definition), Is.True);
            return BuildCardModel.Create(_content, city, definition!);
        }

        /// <summary>A sawmill card names the type, the cost, the build time (as "2m"), the most workers and the recipe with its rate.</summary>
        [Test]
        public void Sawmill_ShowsNameCostTimeWorkersAndRecipe()
        {
            BuildCardModel card = Card("sawmill", BuildMenuTestData.StartCity());

            Assert.That(card.Name, Is.EqualTo("Sawmill"));
            Assert.That(card.CostText, Is.EqualTo("Cost: 150 Coins, 30 Logs"));
            Assert.That(card.BuildTimeText, Is.EqualTo("Build time: 2m"));
            Assert.That(card.WorkersText, Is.EqualTo("Max workers: 3"));
            Assert.That(card.RecipeText, Is.EqualTo("2 Logs → 1 Planks · 60/h at 3 workers"));
        }

        /// <summary>A producer with no input says "Nothing" on the left of the arrow.</summary>
        [Test]
        public void Woodcutter_HasNoInputs()
        {
            BuildCardModel card = Card("woodcutter", BuildMenuTestData.StartCity());

            Assert.That(card.RecipeText, Is.EqualTo("Nothing → 1 Logs · 120/h at 2 workers"));
            Assert.That(card.BuildTimeText, Is.EqualTo("Build time: 1m"));
        }

        /// <summary>A house has no recipe and takes no workers: it says how many it provides, and has no workers line.</summary>
        [Test]
        public void House_SaysWhatItProvides_AndTakesNoWorkers()
        {
            BuildCardModel card = Card("house", BuildMenuTestData.StartCity());

            Assert.That(card.RecipeText, Is.EqualTo("Provides 4 workers"));
            Assert.That(card.WorkersText, Is.Empty);
            Assert.That(card.CostText, Is.EqualTo("Cost: 50 Coins"));
            Assert.That(card.BuildTimeText, Is.EqualTo("Build time: 30s"));
        }

        /// <summary>A labor-reducing research shows in the rate: the card is the engine's display, not a second formula.</summary>
        [Test]
        public void Rate_FollowsTheCitysResearch()
        {
            CityState faster = BuildMenuTestData.City(700, 40, 10, new[] { "sharp_axes" }, 0);

            BuildCardModel card = Card("woodcutter", faster);

            // Labor 60 less 20 % is 48; with 2 workers a cycle is 24 s, so 150 logs an hour.
            Assert.That(card.RecipeText, Is.EqualTo("Nothing → 1 Logs · 150/h at 2 workers"));
        }

        /// <summary>The trading post needs a research: its card is locked and names the research.</summary>
        [Test]
        public void TradingPost_WithoutItsResearch_IsLockedAndNamesIt()
        {
            BuildCardModel card = Card("trading_post", BuildMenuTestData.StartCity());

            Assert.That(card.IsLocked, Is.True);
            Assert.That(card.LockText, Is.EqualTo("Requires: Trade Charter"));
        }

        /// <summary>Once the research is done the card is open and the lock text is gone.</summary>
        [Test]
        public void TradingPost_WithItsResearch_IsOpen()
        {
            BuildCardModel card = Card("trading_post", BuildMenuTestData.City(700, 40, 30, new[] { "trade_charter" }, 0));

            Assert.That(card.IsLocked, Is.False);
            Assert.That(card.LockText, Is.Empty);
        }

        /// <summary>A building available at the start is never locked.</summary>
        [TestCase("house")]
        [TestCase("woodcutter")]
        [TestCase("sawmill")]
        public void StartingTypes_AreNotLocked(string typeId)
        {
            Assert.That(Card(typeId, BuildMenuTestData.StartCity()).IsLocked, Is.False);
        }

        /// <summary>With the stock and a builder the card has no hint.</summary>
        [Test]
        public void Affordable_WithAFreeBuilder_HasNoHint()
        {
            BuildCardModel card = Card("sawmill", BuildMenuTestData.StartCity());

            Assert.That(card.Hint, Is.EqualTo(BuildHint.None));
            Assert.That(card.HintText, Is.Empty);
        }

        /// <summary>Not enough of an item: the hint names it, and only the items that are short.</summary>
        [Test]
        public void Unaffordable_NamesTheMissingItems()
        {
            // The sawmill costs 150 coins and 30 logs.
            BuildCardModel logsShort = Card("sawmill", BuildMenuTestData.City(700, 29, 10, Array.Empty<string>(), 0));
            BuildCardModel bothShort = Card("sawmill", BuildMenuTestData.City(149, 29, 10, Array.Empty<string>(), 0));
            BuildCardModel exact = Card("sawmill", BuildMenuTestData.City(150, 30, 0, Array.Empty<string>(), 0));

            Assert.That(logsShort.Hint, Is.EqualTo(BuildHint.CannotAfford));
            Assert.That(logsShort.HintText, Is.EqualTo("Not enough Logs"));
            Assert.That(bothShort.HintText, Is.EqualTo("Not enough Coins, Logs"));
            Assert.That(exact.Hint, Is.EqualTo(BuildHint.None), "exactly the cost is affordable");
        }

        /// <summary>Every builder busy: the hint says so (two builders in the content), even when the stock would cover the cost.</summary>
        [Test]
        public void AllBuildersBusy_HintsNoFreeBuilder()
        {
            CityState oneBusy = BuildMenuTestData.City(700, 40, 10, Array.Empty<string>(), 1);
            CityState bothBusy = BuildMenuTestData.City(700, 40, 10, Array.Empty<string>(), 2);

            Assert.That(Card("house", oneBusy).Hint, Is.EqualTo(BuildHint.None));
            Assert.That(Card("house", bothBusy).Hint, Is.EqualTo(BuildHint.NoFreeBuilder));
            Assert.That(Card("house", bothBusy).HintText, Is.EqualTo("All builders are busy"));
        }

        /// <summary>The builder is the first hint, as on the server (it checks the builder before the cost).</summary>
        [Test]
        public void NoBuilderAndNoMoney_HintsTheBuilderFirst()
        {
            CityState city = BuildMenuTestData.City(0, 0, 0, Array.Empty<string>(), 2);

            Assert.That(Card("house", city).Hint, Is.EqualTo(BuildHint.NoFreeBuilder));
        }

        /// <summary>A locked card shows its lock and no hint: the player cannot build it, so a price warning would only confuse.</summary>
        [Test]
        public void LockedCard_HasNoHint()
        {
            BuildCardModel card = Card("trading_post", BuildMenuTestData.City(0, 0, 0, Array.Empty<string>(), 2));

            Assert.That(card.IsLocked, Is.True);
            Assert.That(card.Hint, Is.EqualTo(BuildHint.None));
            Assert.That(card.HintText, Is.Empty);
        }

        /// <summary>Two cards made from the same content and city say the same, and a changed hint makes them differ.</summary>
        [Test]
        public void Equality_FollowsTheTexts()
        {
            BuildCardModel a = Card("sawmill", BuildMenuTestData.StartCity());
            BuildCardModel same = Card("sawmill", BuildMenuTestData.StartCity());
            BuildCardModel poorer = Card("sawmill", BuildMenuTestData.City(10, 40, 10, Array.Empty<string>(), 0));

            Assert.That(a, Is.EqualTo(same));
            Assert.That(a.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(poorer));
        }

        /// <summary>A rate in thousandths shows whole, or with the decimals it needs and no trailing zeros.</summary>
        [TestCase(60000L, "60/h")]
        [TestCase(0L, "0/h")]
        [TestCase(2500L, "2.5/h")]
        [TestCase(333L, "0.333/h")]
        [TestCase(1050L, "1.05/h")]
        public void PerHour_ShowsWholeOrDecimals(long milli, string expected)
        {
            Assert.That(BuildCardModel.PerHour(milli), Is.EqualTo(expected));
        }
    }
}
