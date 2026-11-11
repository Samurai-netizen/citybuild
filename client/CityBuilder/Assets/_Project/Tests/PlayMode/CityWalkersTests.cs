#nullable enable

using System;
using System.IO;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The couriers and trip workers on the map (CR-0008-T2 part D): where a walker stands at a moment of its walk, what a resting courier does,
    /// that a load shows, that views are pooled, and that a frame in which nothing changes allocates nothing.
    /// </summary>
    public sealed class CityWalkersTests
    {
        /// <summary>The station's id.</summary>
        private const int StationId = 1;

        /// <summary>The game content.</summary>
        private GameContent _content = null!;

        /// <summary>The object the walkers hang under.</summary>
        private GameObject _host = null!;

        /// <summary>The walkers under test.</summary>
        private CityWalkers _walkers = null!;

        /// <summary>Before each test: the content, a host object and the walkers.</summary>
        [SetUp]
        public void CreateWalkers()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures", "content.json"));
            ContentResponseDto dto = new ApiJson().Deserialize<ContentResponseDto>(json)!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(dto));
            Assert.That(built.IsValid, Is.True);
            _content = built.Content!;
            _host = new GameObject("Test walkers host");
            _walkers = new CityWalkers(_host.transform, _content);
        }

        /// <summary>After each test: destroys the walkers and the host.</summary>
        [TearDown]
        public void DestroyWalkers()
        {
            _walkers.Destroy();
            Object.DestroyImmediate(_host);
        }

        /// <summary>A trip worker is at the source at departure, half way along at half the time, and carries the trip's goods.</summary>
        [Test]
        public void Trip_StandsAtTheSourceThenWalksToTheTargetWithItsLoad()
        {
            _walkers.Show(City(trips: new[] { new Trip(1, StationId, 2, new ItemId("logs"), 5, 100, 115) }));

            _walkers.Tick(0.016f, 100.0);
            WalkerView view = _walkers.TripView(1)!;
            Assert.That(view.Root.transform.localPosition.x, Is.EqualTo(0.5f).Within(1e-4f));

            _walkers.Tick(0.016f, 107.5);
            Assert.That(view.Root.transform.localPosition.x, Is.EqualTo(2.0f).Within(1e-4f));
            Assert.That(view.Root.transform.localPosition.z, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(view.LoadItem, Is.EqualTo("logs"));
            Assert.That(view.HasLoad, Is.True);
        }

        /// <summary>A courier walking to stop 1 goes from its station to the stop; a resting courier stands at the station.</summary>
        [Test]
        public void Courier_WalksFromTheStationAndRestsAtIt()
        {
            _walkers.Show(City(couriers: new[]
            {
                new Courier(StationId, 1, CourierState.Walking, 1, 100, 115, 600),
                new Courier(StationId, 2, CourierState.Resting, 0, null, null, 0),
            }));

            _walkers.Tick(0.016f, 107.5);

            Assert.That(_walkers.IsCourierWalking(StationId, 1), Is.True);
            Assert.That(_walkers.IsCourierWalking(StationId, 2), Is.False);
            Assert.That(_walkers.CourierView(StationId, 1)!.Root.transform.localPosition.x, Is.EqualTo(2.0f).Within(1e-4f));
            Vector3 resting = _walkers.CourierView(StationId, 2)!.Root.transform.localPosition;
            Assert.That(resting.x, Is.EqualTo(0.5f + 0.22f).Within(1e-4f));
            Assert.That(resting.z, Is.EqualTo(0.5f).Within(1e-4f));
        }

        /// <summary>A courier walking home after its last stop starts the walk at the stop whose walk to the station has the walk's length.</summary>
        [Test]
        public void ReturningCourier_StartsAtTheStopThatFitsTheWalk()
        {
            // Stop 2 stands at column 6: six ground steps, 30 s, back to the station at column 0.
            _walkers.Show(City(couriers: new[] { new Courier(StationId, 1, CourierState.Returning, 0, 200, 230, 0) }));

            _walkers.Tick(0.016f, 200.0);

            Assert.That(_walkers.CourierView(StationId, 1)!.Root.transform.localPosition.x, Is.EqualTo(6.5f).Within(1e-4f));
        }

        /// <summary>A courier carrying goods shows the first load.</summary>
        [Test]
        public void Courier_WithAHeldLoad_ShowsIt()
        {
            var load = new CourierLoad(new ItemId("planks"), 2, 4);
            _walkers.Show(City(couriers: new[] { new Courier(StationId, 1, CourierState.Resting, 0, null, null, 0, new[] { load }) }));

            WalkerView view = _walkers.CourierView(StationId, 1)!;

            Assert.That(view.LoadItem, Is.EqualTo("planks"));
            Assert.That(view.HasLoad, Is.True);
        }

        /// <summary>A trip that ended is gone, and the next trip reuses its view.</summary>
        [Test]
        public void Views_AreRecycledThroughThePool()
        {
            _walkers.Show(City(trips: new[] { new Trip(1, 2, 3, new ItemId("logs"), 5, 100, 115) }));
            GameObject first = _walkers.TripView(1)!.Root;

            _walkers.Show(City());
            Assert.That(_walkers.Count, Is.EqualTo(0));
            Assert.That(first.activeSelf, Is.False);

            _walkers.Show(City(trips: new[] { new Trip(2, 2, 3, new ItemId("logs"), 5, 120, 135) }));
            Assert.That(_walkers.TripView(2)!.Root, Is.SameAs(first));
            Assert.That(first.activeSelf, Is.True);
        }

        /// <summary>With a walk under way, showing the same city again and ticking the frame allocates nothing.</summary>
        [Test]
        public void ShowAndTick_WhenNothingChanges_AllocateNothing()
        {
            CityState city = City(
                trips: new[] { new Trip(1, 2, 3, new ItemId("logs"), 5, 100, 115) },
                couriers: new[] { new Courier(StationId, 1, CourierState.Walking, 1, 100, 115, 600) });
            _walkers.Show(city);
            _walkers.Tick(0.016f, 101.0);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 50; frame++)
            {
                _walkers.Show(city);
                _walkers.Tick(0.016f, 101.0 + (frame * 0.016));
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0), "Walkers must not allocate on a frame where no walk starts.");
        }

        /// <summary>
        /// A city with the station (id 1, column 0), two cottages (ids 2 and 3, columns 3 and 6), and a route for slot 1 through the cottages.
        /// </summary>
        private static CityState City(Trip[]? trips = null, Courier[]? couriers = null)
        {
            Building station = new Building(StationId, new BuildingTypeId("courier_station"), 0, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            Building first = new Building(2, new BuildingTypeId("house"), 3, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            Building second = new Building(3, new BuildingTypeId("house"), 6, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var take = new RouteAction(RouteActionKind.Take, new ItemId("logs"), Amount.Preset(new AmountPresetId("all")));
            var route = new Route(StationId, 1, new[] { new RouteStop(2, new[] { take }), new RouteStop(3, new[] { take }) });
            return new CityState(
                100, 1, 100, 0, new[] { station, first, second }, new ResearchId[0], null, null, trips, 10, couriers, new[] { route });
        }
    }
}
