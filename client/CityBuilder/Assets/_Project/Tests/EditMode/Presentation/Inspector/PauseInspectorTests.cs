#nullable enable

using System;
using CityBuilder.Client.Presentation.Inspector;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Inspector
{
    /// <summary>
    /// The Pause / Resume button of the building inspector (CR-0008 amendment 2, D23 and D24): which buildings have it, its text, the pending
    /// state, and its note (a producer finishing its batch, a paused courier station's wait until its couriers are home).
    /// </summary>
    public sealed class PauseInspectorTests
    {
        /// <summary>No command on its way.</summary>
        private static readonly InspectorPending Nothing = new InspectorPending(null, false);

        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>A producer and a courier station have the button, reading Pause; a house and a building under construction don't.</summary>
        [Test]
        public void Button_OnProducersAndStationsOnly()
        {
            Building woodcutter = Woodcutter(paused: false, BuildingPhase.Idle);
            Building station = Station(paused: false);
            Building house = HudTestData.House(3, 2);
            Building site = new Building(4, new BuildingTypeId("woodcutter"), 3, 0, 1, 0, BuildingPhase.Constructing, 0, 100, 0, 0, 0, 0);

            Assert.That(Model(woodcutter, Nothing).HasPause, Is.True);
            Assert.That(Model(woodcutter, Nothing).PauseText, Is.EqualTo("Pause"));
            Assert.That(Model(woodcutter, Nothing).CanPause, Is.True);
            Assert.That(Model(station, Nothing).HasPause, Is.True);
            Assert.That(Model(house, Nothing).HasPause, Is.False);
            Assert.That(Model(site, Nothing).HasPause, Is.False);
        }

        /// <summary>A paused producer reads Resume; while it still runs its cycle the note says it stops after this batch.</summary>
        [Test]
        public void PausedProducer_ReadsResume_AndFinishesItsBatch()
        {
            BuildingInspectorModel idle = Model(Woodcutter(paused: true, BuildingPhase.Idle), Nothing);
            BuildingInspectorModel producing = Model(Woodcutter(paused: true, BuildingPhase.Producing), Nothing);

            Assert.That(idle.PauseText, Is.EqualTo("Resume"));
            Assert.That(idle.StatusText, Is.EqualTo("Paused"));
            Assert.That(idle.PauseNote, Is.Empty);
            Assert.That(producing.PauseNote, Is.EqualTo("Stops after this batch"));
        }

        /// <summary>A pause on its way shows the state it will give, turns the button off and says so.</summary>
        [Test]
        public void PendingPause_ShowsTheComingStateAndTurnsTheButtonOff()
        {
            BuildingInspectorModel model = Model(Woodcutter(paused: false, BuildingPhase.Idle), new InspectorPending(null, false, pause: true));

            Assert.That(model.PauseText, Is.EqualTo("Resume"));
            Assert.That(model.CanPause, Is.False);
            Assert.That(model.PauseNote, Is.EqualTo("Pausing…"));
        }

        /// <summary>A paused station with a courier still walking counts down until it is home; with every courier home it says so.</summary>
        [Test]
        public void PausedStation_CountsDownUntilItsCouriersAreHome()
        {
            Building station = Station(paused: true);
            Building woodcutter = Woodcutter(paused: false, BuildingPhase.Idle);
            var line = new DeliveryLine(1, 1, 1, 2, 3, new ItemId("logs"), 5);
            var walking = new Courier(1, 1, CourierState.Walking, 1, 0, 30, 600);
            var home = new Courier(1, 1, CourierState.AtStation, 0, null, null, 600);

            BuildingInspectorModel away = Model(station, Nothing, new[] { station, woodcutter, HudTestData.House(3, 4) }, new[] { walking }, new[] { line });
            BuildingInspectorModel back = Model(station, Nothing, new[] { station, woodcutter, HudTestData.House(3, 4) }, new[] { home }, new[] { line });

            Assert.That(away.PauseNote, Does.StartWith("Couriers home in "));
            Assert.That(back.PauseNote, Is.EqualTo("All couriers are home"));
            Assert.That(back.StatusText, Is.EqualTo("Paused"));
        }

        /// <summary>A woodcutter (id 2) at (2,0) with 1 worker, paused or not, idle or in a cycle of 60 worker-seconds.</summary>
        private static Building Woodcutter(bool paused, BuildingPhase phase)
        {
            return phase == BuildingPhase.Producing
                ? new Building(2, new BuildingTypeId("woodcutter"), 2, 0, 1, 1, BuildingPhase.Producing, 0, 60, 0, 60, 60, 0, null, null, paused)
                : new Building(2, new BuildingTypeId("woodcutter"), 2, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, null, null, paused);
        }

        /// <summary>A built courier station (id 1) at (0,0) with 1 courier, paused or not.</summary>
        private static Building Station(bool paused)
        {
            return new Building(1, new BuildingTypeId("courier_station"), 0, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, null, null, paused);
        }

        /// <summary>The model of <paramref name="building"/> alone in a city (or with <paramref name="buildings"/>, couriers and lines), commands allowed.</summary>
        private BuildingInspectorModel Model(
            Building building, InspectorPending pending, Building[]? buildings = null, Courier[]? couriers = null, DeliveryLine[]? lines = null)
        {
            Building[] all = buildings ?? new[] { building };
            var city = new CityState(
                0, 1, 10, 700, all, Array.Empty<ResearchId>(), null, null, null, 1, couriers ?? Array.Empty<Courier>(), Array.Empty<Route>(),
                lines, lines == null || lines.Length == 0 ? 1 : 10);
            BuildingInspectorModel? model = BuildingInspectorModel.Create(_content, city, building, pending, CommandAvailability.Allowed);
            Assert.That(model, Is.Not.Null);
            return model!;
        }
    }
}
