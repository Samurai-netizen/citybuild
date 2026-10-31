#nullable enable

using System;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Build
{
    /// <summary>The build menu without a screen (M0-45): one card per type, selection that refuses a locked card, and cards that follow the city.</summary>
    public sealed class BuildMenuModelTests
    {
        /// <summary>The trading post's id in the fixtures' content.</summary>
        private static readonly BuildingTypeId TradingPost = new BuildingTypeId("trading_post");

        /// <summary>The sawmill's id.</summary>
        private static readonly BuildingTypeId Sawmill = new BuildingTypeId("sawmill");

        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = BuildMenuTestData.Content();
        }

        /// <summary>One card per building type of the content, in the content's order.</summary>
        [Test]
        public void Cards_AreOnePerTypeInTheContentsOrder()
        {
            var menu = new BuildMenuModel(_content, BuildMenuTestData.StartCity());

            Assert.That(menu.Cards.Count, Is.EqualTo(_content.Buildings.Count));
            for (int index = 0; index < menu.Cards.Count; index++)
            {
                Assert.That(menu.Cards[index].TypeId, Is.EqualTo(_content.Buildings[index].Id));
            }
        }

        /// <summary>A card of an open type can be selected; a second tap on it deselects it.</summary>
        [Test]
        public void TryToggle_SelectsAnOpenCard_AndTheSecondTapClearsIt()
        {
            var menu = new BuildMenuModel(_content, BuildMenuTestData.StartCity());
            int raised = 0;
            menu.SelectionChanged += () => raised++;

            Assert.That(menu.TryToggle(Sawmill), Is.True);
            Assert.That(menu.Selected, Is.EqualTo(Sawmill));
            Assert.That(menu.TryToggle(Sawmill), Is.True);
            Assert.That(menu.Selected, Is.Null);
            Assert.That(raised, Is.EqualTo(2));
        }

        /// <summary>A locked card cannot be selected, and a type the content does not have cannot either; nothing is raised.</summary>
        [Test]
        public void TryToggle_RefusesALockedCard_AndAnUnknownType()
        {
            var menu = new BuildMenuModel(_content, BuildMenuTestData.StartCity());
            int raised = 0;
            menu.SelectionChanged += () => raised++;

            Assert.That(menu.TryToggle(TradingPost), Is.False);
            Assert.That(menu.TryToggle(new BuildingTypeId("castle")), Is.False);
            Assert.That(menu.Selected, Is.Null);
            Assert.That(raised, Is.Zero);
        }

        /// <summary>A card that warns (cannot afford, no builder) can still be selected: the hint never blocks, the server decides.</summary>
        [Test]
        public void TryToggle_AcceptsACardWithAHint()
        {
            var menu = new BuildMenuModel(_content, BuildMenuTestData.City(0, 0, 0, Array.Empty<string>(), 2));

            Assert.That(menu.Cards[0].Hint, Is.Not.EqualTo(BuildHint.None));
            Assert.That(menu.TryToggle(Sawmill), Is.True);
            Assert.That(menu.Selected, Is.EqualTo(Sawmill));
        }

        /// <summary>The cancel clears the selection and says so once.</summary>
        [Test]
        public void ClearSelection_ClearsOnce()
        {
            var menu = new BuildMenuModel(_content, BuildMenuTestData.StartCity());
            menu.TryToggle(Sawmill);
            int raised = 0;
            menu.SelectionChanged += () => raised++;

            menu.ClearSelection();
            menu.ClearSelection();

            Assert.That(menu.Selected, Is.Null);
            Assert.That(raised, Is.EqualTo(1));
        }

        /// <summary>A research that finishes opens the card: the next update unlocks it and raises the change; selecting works then.</summary>
        [Test]
        public void Update_AfterTheResearch_UnlocksTheCard()
        {
            var menu = new BuildMenuModel(_content, BuildMenuTestData.StartCity());
            int raised = 0;
            menu.CardsChanged += () => raised++;

            menu.Update(BuildMenuTestData.City(700, 40, 10, new[] { "trade_charter" }, 0));

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(CardOf(menu, TradingPost).IsLocked, Is.False);
            Assert.That(menu.TryToggle(TradingPost), Is.True);
        }

        /// <summary>An update that changes no card says nothing, so the view does no work for the second-by-second refresh.</summary>
        [Test]
        public void Update_WithNothingChanged_RaisesNothing()
        {
            var menu = new BuildMenuModel(_content, BuildMenuTestData.StartCity());
            int raised = 0;
            menu.CardsChanged += () => raised++;

            menu.Update(BuildMenuTestData.StartCity());

            Assert.That(raised, Is.Zero);
        }

        /// <summary>The hints follow the city: spending the coins makes the card warn.</summary>
        [Test]
        public void Update_FollowsTheStock()
        {
            var menu = new BuildMenuModel(_content, BuildMenuTestData.StartCity());
            Assert.That(CardOf(menu, Sawmill).Hint, Is.EqualTo(BuildHint.None));

            menu.Update(BuildMenuTestData.City(100, 40, 10, Array.Empty<string>(), 0));

            Assert.That(CardOf(menu, Sawmill).Hint, Is.EqualTo(BuildHint.CannotAfford));
        }

        /// <summary>A selected type that is locked by a later city is deselected, so the placement flow never works from a stale pick.</summary>
        [Test]
        public void Update_DeselectsATypeThatIsLockedNow()
        {
            var menu = new BuildMenuModel(_content, BuildMenuTestData.City(700, 40, 30, new[] { "trade_charter" }, 0));
            menu.TryToggle(TradingPost);

            menu.Update(BuildMenuTestData.StartCity());

            Assert.That(menu.Selected, Is.Null);
        }

        /// <summary>The card of <paramref name="typeId"/>.</summary>
        private static BuildCardModel CardOf(BuildMenuModel menu, BuildingTypeId typeId)
        {
            for (int index = 0; index < menu.Cards.Count; index++)
            {
                if (menu.Cards[index].TypeId.Equals(typeId))
                {
                    return menu.Cards[index];
                }
            }

            throw new AssertionException("No card for " + typeId);
        }
    }
}
