#nullable enable

using System.Collections.Generic;
using System.Linq;
using CityBuilder.Client.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// <see cref="ClientCityStore"/> and <see cref="CitySnapshot"/> (M0-39): which snapshot the store keeps, when it says so, how long ago it arrived,
    /// and that nothing that leaves a snapshot can change it.
    /// </summary>
    public sealed class ClientCityStoreTests
    {
        /// <summary>The fake real-time clock the store reads, in seconds.</summary>
        private double _now;

        /// <summary>How many times the store raised <see cref="ClientCityStore.SnapshotChanged"/>.</summary>
        private int _changes;

        /// <summary>The store under test.</summary>
        private ClientCityStore _store = null!;

        /// <summary>Before each test: an empty store at real-time second 100 that counts its changes.</summary>
        [SetUp]
        public void CreateStore()
        {
            _now = 100;
            _changes = 0;
            _store = new ClientCityStore(() => _now);
            _store.SnapshotChanged += () => _changes++;
        }

        /// <summary>An empty store has no snapshot and no time since it.</summary>
        [Test]
        public void NewStore_HasNothing()
        {
            Assert.That(_store.Snapshot, Is.Null);
            Assert.That(_store.WholeSecondsSinceReceived, Is.EqualTo(0L));
        }

        /// <summary>The first snapshot is taken, stamped with the clock, and announced once.</summary>
        [Test]
        public void Apply_FirstSnapshot_IsHeldAndAnnounced()
        {
            CitySnapshot snapshot = CityTestData.EmptySnapshot(5, 50);

            Assert.That(_store.Apply(snapshot), Is.True);

            Assert.That(_store.Snapshot, Is.SameAs(snapshot));
            Assert.That(_store.ReceivedAtSeconds, Is.EqualTo(100d));
            Assert.That(_changes, Is.EqualTo(1));
        }

        /// <summary>A higher version replaces the snapshot and restarts the time since it.</summary>
        [Test]
        public void Apply_NewerVersion_Replaces()
        {
            _store.Apply(CityTestData.EmptySnapshot(5, 50));
            _now = 130;
            CitySnapshot newer = CityTestData.EmptySnapshot(6, 80);

            Assert.That(_store.Apply(newer), Is.True);

            Assert.That(_store.Snapshot, Is.SameAs(newer));
            Assert.That(_store.WholeSecondsSinceReceived, Is.EqualTo(0L));
            Assert.That(_changes, Is.EqualTo(2));
        }

        /// <summary>A lower version, an out-of-order response, changes nothing and announces nothing, even with a later clock.</summary>
        [Test]
        public void Apply_OlderVersion_IsIgnored()
        {
            CitySnapshot held = CityTestData.EmptySnapshot(5, 50);
            _store.Apply(held);
            _now = 130;

            Assert.That(_store.Apply(CityTestData.EmptySnapshot(4, 90)), Is.False);

            Assert.That(_store.Snapshot, Is.SameAs(held));
            Assert.That(_store.ReceivedAtSeconds, Is.EqualTo(100d));
            Assert.That(_changes, Is.EqualTo(1));
        }

        /// <summary>The same version with a later clock is a later reading of the city: taken.</summary>
        [Test]
        public void Apply_SameVersionLaterClock_Replaces()
        {
            _store.Apply(CityTestData.EmptySnapshot(5, 50));

            Assert.That(_store.Apply(CityTestData.EmptySnapshot(5, 51)), Is.True);

            Assert.That(_store.Snapshot!.Clock, Is.EqualTo(51L));
        }

        /// <summary>
        /// The same version at the same clock is the same city, like a replayed command's stored answer arriving late. Taking it would restart
        /// the time since the snapshot and move the display clock back, so the store keeps the receive time it has.
        /// </summary>
        [Test]
        public void Apply_SameVersionSameClock_KeepsTheReceiveTime()
        {
            CitySnapshot held = CityTestData.EmptySnapshot(5, 50);
            _store.Apply(held);
            _now = 190;

            Assert.That(_store.Apply(CityTestData.EmptySnapshot(5, 50)), Is.False);

            Assert.That(_store.Snapshot, Is.SameAs(held));
            Assert.That(_store.WholeSecondsSinceReceived, Is.EqualTo(90L));
            Assert.That(_changes, Is.EqualTo(1));
        }

        /// <summary>The time since the snapshot counts whole seconds only, and a clock that went back counts as none.</summary>
        [TestCase(0.0, 0L)]
        [TestCase(0.999, 0L)]
        [TestCase(1.0, 1L)]
        [TestCase(61.5, 61L)]
        [TestCase(-5.0, 0L)]
        public void WholeSeconds_RoundDownAndNeverGoBelowZero(double later, long expected)
        {
            _store.Apply(CityTestData.EmptySnapshot(5, 50));
            _now = 100 + later;

            Assert.That(_store.WholeSecondsSinceReceived, Is.EqualTo(expected));
        }

        /// <summary>A copy holds every value of the city it was made from, for a building in each phase, with the research running and completed, in new objects.</summary>
        [Test]
        public void CopyCity_HoldsEveryValueInNewObjects()
        {
            var constructing = new Building(1, new BuildingTypeId("sawmill"), 0, 0, 1, 0, BuildingPhase.Constructing, 5, 15, 0, 0, 0, 0);
            var upgrading = new Building(2, new BuildingTypeId("house"), 1, 2, 3, 0, BuildingPhase.Upgrading, 6, 16, 4, 0, 0, 0);
            var idle = new Building(4, new BuildingTypeId("house"), 3, 4, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var producing = new Building(7, new BuildingTypeId("woodcutter"), 5, 6, 2, 2, BuildingPhase.Producing, 8, 30, 0, 100, 40, 10);
            var frozen = new Building(8, new BuildingTypeId("woodcutter"), 7, 7, 1, 0, BuildingPhase.Producing, 8, null, 0, 90, 33, 12);
            var stock = new[] { new KeyValuePair<ItemId, long>(new ItemId("coins"), 640), new KeyValuePair<ItemId, long>(new ItemId("logs"), 37) };
            var city = new CityState(
                20,
                2,
                11,
                stock,
                new[] { constructing, upgrading, idle, producing, frozen },
                new[] { new ResearchId("sharp_axes") },
                new ActiveResearch(new ResearchId("trade_charter"), 4, 604));
            string expected = CityTestData.Describe(city);

            CityState copy = new CitySnapshot(1, "mvp-1", city).CopyCity();

            Assert.That(CityTestData.Describe(copy), Is.EqualTo(expected));
            Assert.That(copy, Is.Not.SameAs(city));
            Assert.That(copy.Buildings.Zip(city.Buildings, (a, b) => ReferenceEquals(a, b)), Has.None.True);
        }

        /// <summary>Running the engine on a copy, buildings and stock included, leaves the snapshot as it was: the state the store holds is immutable.</summary>
        [Test]
        public void CopyCity_AdvancingACopy_LeavesTheSnapshotAlone()
        {
            GameContent content = CityTestData.Content();
            CitySnapshot snapshot = CityTestData.Snapshot("snapshot-city-producing.json");
            string before = CityTestData.Describe(snapshot.CopyCity());

            CityState copy = snapshot.CopyCity();
            SettlementReport report = Simulator.Advance(copy, content, 1000);

            Assert.That(report.CompletedResearch, Is.Not.Empty, "The advance must have changed the copy for this test to mean anything.");
            Assert.That(copy.GetQuantity(new ItemId("planks")), Is.Not.EqualTo(10L));
            Assert.That(CityTestData.Describe(snapshot.CopyCity()), Is.EqualTo(before));
        }
    }
}
