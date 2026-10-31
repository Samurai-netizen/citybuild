#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CityBuilder.Client.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// <see cref="DisplayClock"/> and <see cref="DisplayCity"/> (M0-39): the prediction equals one <see cref="Simulator.Advance"/> of the whole time,
    /// runs at most once per second, and gives way to every new snapshot at once.
    /// </summary>
    public sealed class DisplayCityTests
    {
        /// <summary>The real answers the exact-match test runs on.</summary>
        private static readonly string[] SnapshotFixtures =
        {
            "snapshot-city-start.json",
            "snapshot-city-producing.json",
            "snapshot-city-research-completed.json",
            "snapshot-city-settled-buildings.json",
            "snapshot-sawmill-constructing.json",
            "snapshot-command-assign-workers.json",
        };

        /// <summary>The real content.</summary>
        private GameContent _content = null!;

        /// <summary>The fake real-time clock, in seconds.</summary>
        private double _now;

        /// <summary>The store the prediction reads.</summary>
        private ClientCityStore _store = null!;

        /// <summary>The clock at the normal speed.</summary>
        private DisplayClock _clock = null!;

        /// <summary>Before each test: an empty store on a fake clock at second 100.</summary>
        [SetUp]
        public void CreateStore()
        {
            _content = CityTestData.Content();
            _now = 100;
            _store = new ClientCityStore(() => _now);
            _clock = new DisplayClock(_store, 1);
        }

        /// <summary>The predicted city after N seconds is exactly what the engine makes of the snapshot in one advance of N, for any N and any real answer.</summary>
        [Test]
        public void Prediction_AfterNSeconds_EqualsOneAdvanceExactly(
            [ValueSource(nameof(SnapshotFixtures))] string fixture,
            [Values(0, 1, 7, 59, 60, 405, 406, 3600, 28800, 40000)] int seconds)
        {
            _store.Apply(CityTestData.Snapshot(fixture));
            var display = new DisplayCity(_store, _clock, _content);

            _now += seconds;
            display.Refresh();

            CityState expected = CityTestData.Snapshot(fixture).CopyCity();
            Simulator.Advance(expected, _content, seconds);
            Assert.That(CityTestData.Describe(display.Current!), Is.EqualTo(CityTestData.Describe(expected)));
            Assert.That(display.Current!.Clock, Is.EqualTo(_clock.Now));
        }

        /// <summary>
        /// Refreshing all along gives the same city as one late refresh, past the 8-hour production cap too: each prediction starts again from the
        /// snapshot, so the dormant tail is measured from the snapshot as on the server, not from the last prediction.
        /// </summary>
        [Test]
        public void Prediction_RefreshedAlongTheWay_EqualsOneAdvanceOfTheWholeTime()
        {
            CitySnapshot snapshot = CityTestData.Snapshot("snapshot-city-producing.json");
            _store.Apply(snapshot);
            var display = new DisplayCity(_store, _clock, _content);
            long whole = _content.Rules.MaxOfflineProductionSeconds + 7200;

            for (long seconds = 3600; seconds <= whole; seconds += 3600)
            {
                _now = 100 + seconds;
                display.Refresh();
            }

            CityState expected = snapshot.CopyCity();
            Simulator.Advance(expected, _content, whole);
            Assert.That(CityTestData.Describe(display.Current!), Is.EqualTo(CityTestData.Describe(expected)));
        }

        /// <summary>The running research completes in the prediction at its end second and not one second before.</summary>
        [Test]
        public void Prediction_ResearchCompletesExactlyOnTime()
        {
            // The fixture's clock is 199 and trade_charter ends at 604.
            CitySnapshot snapshot = CityTestData.Snapshot("snapshot-city-producing.json");
            _store.Apply(snapshot);
            var display = new DisplayCity(_store, _clock, _content);
            var charter = new ResearchId("trade_charter");

            _now = 100 + 404;
            display.Refresh();
            Assert.That(display.Current!.ActiveResearch!.Id, Is.EqualTo(charter));
            Assert.That(display.Current.HasCompletedResearch(charter), Is.False);

            _now = 100 + 405;
            display.Refresh();
            Assert.That(display.Current!.ActiveResearch, Is.Null);
            Assert.That(display.Current.HasCompletedResearch(charter), Is.True);
        }

        /// <summary>A building under construction finishes in the prediction, gets its workers from the free ones and starts producing.</summary>
        [Test]
        public void Prediction_ConstructionCompletes_AutoStaffsAndStartsProducing()
        {
            CityBuilder.Client.Net.SnapshotCityDto dto = CityTestData.Dto("snapshot-sawmill-constructing.json").City;
            CityBuilder.Client.Net.SnapshotBuildingDto sawmill = dto.Buildings.Single(b => b.Id == 3);
            long secondsLeft = sawmill.PhaseEndsAt!.Value - dto.Clock;
            _store.Apply(CityTestData.Snapshot("snapshot-sawmill-constructing.json"));
            var display = new DisplayCity(_store, _clock, _content);
            Assert.That(Building(display, 3).Phase, Is.EqualTo(BuildingPhase.Constructing));
            Assert.That(Building(display, 3).AssignedWorkers, Is.EqualTo(0));

            _now = 100 + secondsLeft - 1;
            display.Refresh();
            Assert.That(Building(display, 3).Phase, Is.EqualTo(BuildingPhase.Constructing));

            _now = 100 + secondsLeft;
            display.Refresh();
            // The fixture has 8 free workers and a sawmill takes 3.
            Assert.That(Building(display, 3).AssignedWorkers, Is.EqualTo(sawmill.MaxWorkers));
            Assert.That(Building(display, 3).AssignedWorkers, Is.EqualTo(3));
            Assert.That(Building(display, 3).Phase, Is.EqualTo(BuildingPhase.Producing));
        }

        /// <summary>Within a second nothing is computed; the next whole second computes once. Calling it every frame is cheap.</summary>
        [Test]
        public void Refresh_AtMostOncePerSecond()
        {
            _store.Apply(CityTestData.Snapshot("snapshot-city-producing.json"));
            var display = new DisplayCity(_store, _clock, _content);
            int changes = 0;
            display.Changed += () => changes++;
            CityState first = display.Current!;

            _now = 100.5;
            Assert.That(display.Refresh(), Is.False);
            _now = 100.999;
            Assert.That(display.Refresh(), Is.False);
            Assert.That(display.Current, Is.SameAs(first));
            Assert.That(changes, Is.EqualTo(0));

            _now = 101;
            Assert.That(display.Refresh(), Is.True);
            CityState second = display.Current!;
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(changes, Is.EqualTo(1));

            _now = 101.5;
            Assert.That(display.Refresh(), Is.False);
            Assert.That(display.Current, Is.SameAs(second));
            Assert.That(changes, Is.EqualTo(1));
        }

        /// <summary>A new snapshot replaces the prediction at once, even a poorer one than was predicted: the server always wins, and the clock restarts from it.</summary>
        [Test]
        public void NewSnapshot_ReplacesThePredictionAtOnce()
        {
            _store.Apply(CityTestData.Snapshot("snapshot-city-producing.json"));
            var display = new DisplayCity(_store, _clock, _content);
            int changes = 0;
            display.Changed += () => changes++;
            _now += 5000;
            display.Refresh();
            Assert.That(display.Current!.Clock, Is.EqualTo(5199L));
            Assert.That(display.Current.HasCompletedResearch(new ResearchId("trade_charter")), Is.True);

            CitySnapshot server = CityTestData.EmptySnapshot(26, 300);
            _store.Apply(server);

            Assert.That(CityTestData.Describe(display.Current!), Is.EqualTo(CityTestData.Describe(server.CopyCity())));
            Assert.That(_clock.Now, Is.EqualTo(300L));
            Assert.That(changes, Is.EqualTo(2));
        }

        /// <summary>A snapshot the store ignores leaves the prediction running.</summary>
        [Test]
        public void OlderSnapshot_LeavesThePredictionAlone()
        {
            _store.Apply(CityTestData.Snapshot("snapshot-city-producing.json"));
            var display = new DisplayCity(_store, _clock, _content);
            _now += 10;
            display.Refresh();
            CityState predicted = display.Current!;

            _store.Apply(CityTestData.EmptySnapshot(3, 300));

            Assert.That(display.Current, Is.SameAs(predicted));
            Assert.That(display.Refresh(), Is.False);
        }

        /// <summary>Predicting never changes what the store holds.</summary>
        [Test]
        public void Prediction_LeavesTheStoreAlone()
        {
            CitySnapshot snapshot = CityTestData.Snapshot("snapshot-city-producing.json");
            string before = CityTestData.Describe(snapshot.CopyCity());
            _store.Apply(snapshot);
            var display = new DisplayCity(_store, _clock, _content);

            _now += 40000;
            display.Refresh();

            Assert.That(_store.Snapshot, Is.SameAs(snapshot));
            Assert.That(CityTestData.Describe(snapshot.CopyCity()), Is.EqualTo(before));
        }

        /// <summary>Before the first snapshot there is nothing to show; the first one is computed when it arrives, without a refresh.</summary>
        [Test]
        public void NoSnapshotYet_ShowsNothingThenTheFirstOne()
        {
            var display = new DisplayCity(_store, _clock, _content);
            Assert.That(display.Current, Is.Null);
            Assert.That(display.Refresh(), Is.False);
            Assert.That(_clock.Now, Is.EqualTo(0L));

            _store.Apply(CityTestData.Snapshot("snapshot-city-start.json"));

            Assert.That(display.Current, Is.Not.Null);
        }

        /// <summary>
        /// A building at a level the content doesn't have (a command's answer that was made with newer content) makes the engine throw another
        /// exception than a missing type does. It is handled the same way: logged once, the snapshot is shown, and a command that brought the
        /// snapshot does not fail because of the display (CR-0004).
        /// </summary>
        [Test]
        public void ALevelTheContentDoesNotHave_ShowsTheSnapshot_WithoutThrowing()
        {
            var unknownLevel = new Building(1, new BuildingTypeId("woodcutter"), 0, 0, 9, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var city = new CityState(50, 1, 2, Array.Empty<KeyValuePair<ItemId, long>>(), new[] { unknownLevel }, Array.Empty<ResearchId>(), null);
            _store.Apply(new CitySnapshot(5, "mvp-2", city));
            LogAssert.Expect(LogType.Error, new Regex("cannot predict state version 5 \\(content mvp-2\\)"));

            var display = new DisplayCity(_store, _clock, _content);
            _now += 3;

            Assert.That(display.Current!.Clock, Is.EqualTo(50L));
            Assert.That(display.Refresh(), Is.False, "No second attempt, so no second error.");
        }

        /// <summary>
        /// A city the engine refuses (here a building this content doesn't know completes after 2 seconds) doesn't take the game down: one error in
        /// the log, the snapshot shown as it is, and no more attempts until the next snapshot, which predicts again.
        /// </summary>
        [Test]
        public void EngineRefusesTheCity_ShowsTheSnapshotAndRecoversWithTheNext()
        {
            var ghost = new Building(1, new BuildingTypeId("ghost"), 0, 0, 1, 0, BuildingPhase.Constructing, 40, 52, 0, 0, 0, 0);
            var city = new CityState(50, 1, 2, Array.Empty<KeyValuePair<ItemId, long>>(), new[] { ghost }, Array.Empty<ResearchId>(), null);
            _store.Apply(new CitySnapshot(5, "mvp-2", city));
            var display = new DisplayCity(_store, _clock, _content);
            LogAssert.Expect(LogType.Error, new Regex("cannot predict state version 5 \\(content mvp-2\\)"));

            _now += 3;
            display.Refresh();

            Assert.That(display.Current!.Clock, Is.EqualTo(50L));
            Assert.That(display.Current.Buildings[0].Phase, Is.EqualTo(BuildingPhase.Constructing));
            _now += 3;
            Assert.That(display.Refresh(), Is.False, "No second attempt, so no second error.");

            _store.Apply(CityTestData.Snapshot("snapshot-city-producing.json"));
            _now += 3;
            Assert.That(display.Refresh(), Is.True);
            Assert.That(display.Current!.Clock, Is.EqualTo(202L));
        }

        /// <summary>The clock is the snapshot's plus the whole seconds since it, times the server clock's speed, and the prediction follows it.</summary>
        [TestCase(1, 2.9, 201L)]
        [TestCase(60, 2.9, 319L)]
        [TestCase(1000, 1.0, 1199L)]
        public void DisplayClock_ScalesWholeSeconds(int timeScale, double later, long expectedClock)
        {
            var clock = new DisplayClock(_store, timeScale);
            _store.Apply(CityTestData.Snapshot("snapshot-city-producing.json"));
            var display = new DisplayCity(_store, clock, _content);

            _now += later;
            display.Refresh();

            Assert.That(clock.Now, Is.EqualTo(expectedClock));
            Assert.That(display.Current!.Clock, Is.EqualTo(expectedClock));
        }

        /// <summary>A clock that doesn't run forward is a programming error.</summary>
        [TestCase(0)]
        [TestCase(-1)]
        public void DisplayClock_RefusesAScaleBelowOne(int timeScale)
        {
            Assert.That(() => new DisplayClock(_store, timeScale), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        /// <summary>The predicted building with the given id.</summary>
        private static Building Building(DisplayCity display, int id)
        {
            Assert.That(display.Current!.TryGetBuilding(id, out Building? building), Is.True);
            return building!;
        }
    }
}
