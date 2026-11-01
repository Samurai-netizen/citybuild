#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.Hud;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Hud
{
    /// <summary>The state of the whole HUD (M0-49): the chips, workers, builders, ring, era and connection, and when it says it changed.</summary>
    public sealed class HudModelTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>A city with two cottages (8 workers), a woodcutter (2) and a sawmill (3) at work, and one building under construction.</summary>
        private static CityState WorkingCity(long clock = 0, ActiveResearch? research = null)
        {
            return HudTestData.City(
                clock,
                700,
                100,
                10,
                new[]
                {
                    HudTestData.House(1, 0),
                    HudTestData.House(2, 1),
                    HudTestData.Producer(3, 2, "woodcutter", 2, 60),
                    HudTestData.Producer(4, 3, "sawmill", 3, 180),
                    HudTestData.Constructing(5, 4),
                },
                null,
                research);
        }

        /// <summary>The chips are the items of the city's era in the content's order, named by the content.</summary>
        [Test]
        public void Chips_AreTheItemsOfTheEra_InContentOrder()
        {
            var model = new HudModel(_content, WorkingCity(), ConnectionState.Connected);

            var names = new List<string>();
            foreach (ItemChipModel chip in model.Chips)
            {
                names.Add(chip.Name);
            }

            Assert.That(names, Is.EqualTo(new[] { "Coins", "Logs", "Planks" }));
        }

        /// <summary>Workers are free out of provided: 8 provided by two cottages, 5 held by the woodcutter and the sawmill.</summary>
        [Test]
        public void Workers_AreFreeOutOfProvided()
        {
            var model = new HudModel(_content, WorkingCity(), ConnectionState.Connected);

            Assert.That(model.WorkersText, Is.EqualTo("3 / 8"));
        }

        /// <summary>Builders are busy out of the rules' slots: one construction, two slots.</summary>
        [Test]
        public void Builders_AreBusyOutOfSlots()
        {
            var model = new HudModel(_content, WorkingCity(), ConnectionState.Connected);

            Assert.That(model.BuildersText, Is.EqualTo("1 / 2"));
        }

        /// <summary>The era badge is the era's name from the content.</summary>
        [Test]
        public void EraName_IsFromTheContent()
        {
            var model = new HudModel(_content, WorkingCity(), ConnectionState.Connected);

            Assert.That(model.EraName, Is.EqualTo("Early Middle Ages"));
        }

        /// <summary>Every connection state has its word and level; only a connected client is ok, and only a starting one is merely pending.</summary>
        [TestCase(ConnectionState.Connecting, "Connecting", ConnectionLevel.Pending)]
        [TestCase(ConnectionState.Connected, "Online", ConnectionLevel.Ok)]
        [TestCase(ConnectionState.Offline, "Offline", ConnectionLevel.Problem)]
        [TestCase(ConnectionState.Unauthorized, "Signed out", ConnectionLevel.Problem)]
        [TestCase(ConnectionState.ServerError, "Server problem", ConnectionLevel.Problem)]
        [TestCase(ConnectionState.IncompatibleVersion, "Update needed", ConnectionLevel.Problem)]
        public void Connection_HasAWordAndALevel(ConnectionState state, string text, ConnectionLevel level)
        {
            var model = new HudModel(_content, WorkingCity(), state);

            Assert.That(model.ConnectionText, Is.EqualTo(text));
            Assert.That(model.ConnectionLevel, Is.EqualTo(level));
        }

        /// <summary>An update with the same city and state says nothing changed.</summary>
        [Test]
        public void Update_WithNothingNew_RaisesNothing()
        {
            var model = new HudModel(_content, WorkingCity(), ConnectionState.Connected);
            int raised = 0;
            model.Changed += () => raised++;

            model.Update(WorkingCity(), ConnectionState.Connected);

            Assert.That(raised, Is.EqualTo(0));
        }

        /// <summary>A later clock that changes no shown value (no research, nothing producing) raises nothing.</summary>
        [Test]
        public void Update_WithOnlyTheClockMoved_RaisesNothing()
        {
            CityState quiet = HudTestData.City(0, 700, 40, 10, new[] { HudTestData.House(1, 0) });
            CityState later = HudTestData.City(1, 700, 40, 10, new[] { HudTestData.House(1, 0) });
            var model = new HudModel(_content, quiet, ConnectionState.Connected);
            int raised = 0;
            model.Changed += () => raised++;

            model.Update(later, ConnectionState.Connected);

            Assert.That(raised, Is.EqualTo(0));
        }

        /// <summary>A new stock, a freed worker, a new ring value and a new connection each raise one change.</summary>
        [Test]
        public void Update_WithAChange_RaisesOnceAndShowsIt()
        {
            var model = new HudModel(_content, WorkingCity(), ConnectionState.Connected);
            int raised = 0;
            model.Changed += () => raised++;
            CityState richer = HudTestData.City(
                0,
                900,
                100,
                10,
                new[] { HudTestData.House(1, 0), HudTestData.House(2, 1), HudTestData.Constructing(5, 4) },
                null,
                HudTestData.Research("sharp_axes", 0, 1800));

            model.Update(richer, ConnectionState.Offline);

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(model.Chips[0].StockText, Is.EqualTo("900"));
            Assert.That(model.WorkersText, Is.EqualTo("8 / 8"));
            Assert.That(model.Research.IsActive, Is.True);
            Assert.That(model.ConnectionText, Is.EqualTo("Offline"));
        }

        /// <summary>While research runs, each second changes the time left and raises a change.</summary>
        [Test]
        public void Update_WhileResearching_RaisesEverySecond()
        {
            ActiveResearch research = HudTestData.Research("sharp_axes", 0, 1800);
            var model = new HudModel(_content, WorkingCity(10, research), ConnectionState.Connected);
            int raised = 0;
            model.Changed += () => raised++;

            model.Update(WorkingCity(11, research), ConnectionState.Connected);

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(model.Research.TimeLeftText, Is.EqualTo("29m 49s"));
        }
    }
}
