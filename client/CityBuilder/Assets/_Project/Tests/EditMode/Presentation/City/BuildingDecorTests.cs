#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// What <see cref="BuildingDecor"/> shows on a building's view (CR-0008-T2): how full its own store is against its effective limit, and
    /// whether its moving parts and smoke move for each building kind.
    /// </summary>
    public sealed class BuildingDecorTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>The parent object a test builds, destroyed in <see cref="TearDown"/>.</summary>
        private GameObject _parent = null!;

        /// <summary>The factory that builds a building's view with its fill and animator.</summary>
        private BuildingViewFactory _factory = null!;

        /// <summary>Loads the content and creates a fresh parent and factory before each test.</summary>
        [SetUp]
        public void SetUp()
        {
            _content = HudTestData.Content();
            _parent = new GameObject("Test parent");
            _factory = new BuildingViewFactory(new CityGrid(8, 8));
        }

        /// <summary>Destroys the objects and materials a test built.</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
            _factory.Destroy();
        }

        /// <summary>How many child step objects of a fill view for <paramref name="item"/> are active.</summary>
        /// <param name="fill">The fill view.</param>
        /// <param name="item">The item id prefix (for example <c>Fill_logs_</c>).</param>
        /// <returns>The active step count.</returns>
        private static int ActiveSteps(FillView fill, string item)
        {
            int count = 0;
            string prefix = FillView.StepPrefix + item + "_";
            for (int i = 0; i < fill.Root.childCount; i++)
            {
                Transform child = fill.Root.GetChild(i);
                if (child.name.StartsWith(prefix, StringComparison.Ordinal) && child.gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>A woodcutter (level 1 limit 40 logs) with 0, 1, 14 and 27 logs in its own store shows 0, 1, 2 and 3 steps.</summary>
        /// <param name="logs">The logs in the woodcutter's store.</param>
        /// <param name="expectedSteps">The steps shown.</param>
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(14, 2)]
        [TestCase(27, 3)]
        [TestCase(40, 3)]
        public void Show_ShowsTheBuildingsOwnStoreAgainstItsEffectiveLimit(long logs, int expectedSteps)
        {
            KeyValuePair<ItemId, long>[] store = logs == 0
                ? Array.Empty<KeyValuePair<ItemId, long>>()
                : new[] { new KeyValuePair<ItemId, long>(new ItemId("logs"), logs) };
            var building = new Building(1, new BuildingTypeId("woodcutter"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, store);
            BuildingView view = _factory.Create(building, 1, _parent.transform);
            CityState city = HudTestData.City(0, 100, 0, 0, new[] { building });

            BuildingDecor.Show(view, city, _content, building, BuildingStatusModel.Describe(city, _content, building));

            Assert.That(view.Fill, Is.Not.Null);
            Assert.That(ActiveSteps(view.Fill!, "logs"), Is.EqualTo(expectedSteps));
        }

        /// <summary>An item the building's store has no limit for shows 0 steps even when the fill model has steps for it.</summary>
        [Test]
        public void Show_ForAnItemWithoutALimitOnTheBuilding_ShowsNothing()
        {
            var building = new Building(
                1,
                new BuildingTypeId("woodcutter"),
                0,
                0,
                1,
                0,
                BuildingPhase.Idle,
                0,
                null,
                0,
                0,
                0,
                0,
                new[] { new KeyValuePair<ItemId, long>(new ItemId("planks"), 20) });
            BuildingView view = _factory.Create(building, 1, _parent.transform);
            BuildingDecor.Attach(view, BuildingDecor.FindFill("sawmill", 1, 1));
            CityState city = HudTestData.City(0, 100, 0, 0, new[] { building });

            BuildingDecor.Show(view, city, _content, building, BuildingStatusModel.Describe(city, _content, building));

            Assert.That(view.Fill, Is.Not.Null);
            Assert.That(ActiveSteps(view.Fill!, "planks"), Is.EqualTo(0));
        }

        /// <summary>A construction site that waits for goods stands still; once its build timer runs, it works (its builder hammers).</summary>
        [Test]
        public void IsWorking_ConstructionSiteWaitingForGoods_IsFalse_AndRunningTimer_IsTrue()
        {
            var waiting = new Building(
                1,
                new BuildingTypeId("sawmill"),
                0,
                0,
                1,
                0,
                BuildingPhase.Constructing,
                0,
                null,
                0,
                0,
                0,
                0,
                null,
                new[] { new KeyValuePair<ItemId, long>(new ItemId("logs"), 15) });
            var running = new Building(1, new BuildingTypeId("sawmill"), 0, 0, 1, 0, BuildingPhase.Constructing, 0, 60, 0, 0, 0, 0);
            CityState waitingCity = HudTestData.City(0, 100, 0, 0, new[] { waiting });
            CityState runningCity = HudTestData.City(0, 100, 0, 0, new[] { running });

            Assert.That(BuildingDecor.IsWorking(waitingCity, _content, waiting, BuildingStatusModel.Describe(waitingCity, _content, waiting)), Is.False);
            Assert.That(BuildingDecor.IsWorking(runningCity, _content, running, BuildingStatusModel.Describe(runningCity, _content, running)), Is.True);
        }

        /// <summary>The science building works only while a research timer runs, not when no research runs or when the research waits for goods.</summary>
        [Test]
        public void IsWorking_ScienceBuilding_IsTrueOnlyWhileResearchTimerRuns()
        {
            var scriptorium = new Building(1, new BuildingTypeId("scriptorium"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var idleCity = new CityState(0, 1, 2, 100, new[] { scriptorium }, System.Array.Empty<ResearchId>(), null);
            var waitingResearch = new ActiveResearch(
                new ResearchId("cart_axles"),
                null,
                null,
                new[] { new KeyValuePair<ItemId, long>(new ItemId("planks"), 10) });
            var waitingCity = new CityState(0, 1, 2, 100, new[] { scriptorium }, System.Array.Empty<ResearchId>(), waitingResearch);
            var runningResearch = new ActiveResearch(new ResearchId("forestry"), 0, 60);
            var runningCity = new CityState(0, 1, 2, 100, new[] { scriptorium }, System.Array.Empty<ResearchId>(), runningResearch);
            BuildingStatus status = BuildingStatusModel.Describe(idleCity, _content, scriptorium);

            Assert.That(BuildingDecor.IsWorking(idleCity, _content, scriptorium, status), Is.False);
            Assert.That(BuildingDecor.IsWorking(waitingCity, _content, scriptorium, status), Is.False);
            Assert.That(BuildingDecor.IsWorking(runningCity, _content, scriptorium, status), Is.True);
        }

        /// <summary>A courier station works while one of its couriers is <see cref="CourierState.Walking"/> or <see cref="CourierState.Returning"/>, not while <see cref="CourierState.Resting"/> or <see cref="CourierState.AtStation"/>.</summary>
        /// <param name="state">The courier's state.</param>
        /// <param name="expected">Whether the station works.</param>
        [TestCase(CourierState.AtStation, false)]
        [TestCase(CourierState.Resting, false)]
        [TestCase(CourierState.Walking, true)]
        [TestCase(CourierState.Returning, true)]
        public void IsWorking_CourierStation_FollowsWhetherACourierIsOnTheRoad(CourierState state, bool expected)
        {
            var station = new Building(1, new BuildingTypeId("courier_station"), 0, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var woodcutter = new Building(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var warehouse = new Building(3, new BuildingTypeId("warehouse"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var action = new RouteAction(RouteActionKind.Take, new ItemId("logs"), Amount.Preset(new AmountPresetId("all")));
            var route = new Route(1, 1, new[] { new RouteStop(2, new[] { action }), new RouteStop(3, new[] { action }) });
            bool walks = state == CourierState.Walking || state == CourierState.Returning;
            var courier = new Courier(
                1,
                1,
                state,
                state == CourierState.Walking ? 1 : 0,
                walks ? 0 : (long?)null,
                walks ? 10 : (long?)null,
                100);
            var city = new CityState(
                0,
                1,
                4,
                100,
                new[] { station, woodcutter, warehouse },
                System.Array.Empty<ResearchId>(),
                null,
                null,
                null,
                1,
                new[] { courier },
                new[] { route });

            Assert.That(
                BuildingDecor.IsWorking(city, _content, station, BuildingStatusModel.Describe(city, _content, station)),
                Is.EqualTo(expected));
        }

        /// <summary>A warehouse works while a trip goes from or to it, or a courier walks to a stop at it; otherwise it stands still.</summary>
        [Test]
        public void IsWorking_Warehouse_IsTrueWhileATripOrCourierMovesGoodsThere()
        {
            var station = new Building(1, new BuildingTypeId("courier_station"), 0, 0, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var woodcutter = new Building(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var warehouse = new Building(3, new BuildingTypeId("warehouse"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            Building[] buildings = { station, woodcutter, warehouse };
            var action = new RouteAction(RouteActionKind.Take, new ItemId("logs"), Amount.Preset(new AmountPresetId("all")));
            var route = new Route(1, 1, new[] { new RouteStop(2, new[] { action }), new RouteStop(3, new[] { action }) });

            var stillCity = new CityState(0, 1, 4, 100, buildings, System.Array.Empty<ResearchId>(), null);
            var tripToWarehouse = new CityState(
                0,
                1,
                4,
                100,
                buildings,
                System.Array.Empty<ResearchId>(),
                null,
                null,
                new[] { new Trip(1, 2, 3, new ItemId("logs"), 10, 0, 10) },
                2);
            var courierToWarehouse = new CityState(
                0,
                1,
                4,
                100,
                buildings,
                System.Array.Empty<ResearchId>(),
                null,
                null,
                null,
                1,
                new[] { new Courier(1, 1, CourierState.Walking, 2, 0, 10, 100) },
                new[] { route });
            var courierToWoodcutter = new CityState(
                0,
                1,
                4,
                100,
                buildings,
                System.Array.Empty<ResearchId>(),
                null,
                null,
                null,
                1,
                new[] { new Courier(1, 1, CourierState.Walking, 1, 0, 10, 100) },
                new[] { route });
            BuildingStatus status = BuildingStatusModel.Describe(stillCity, _content, warehouse);

            Assert.That(BuildingDecor.IsWorking(stillCity, _content, warehouse, status), Is.False);
            Assert.That(BuildingDecor.IsWorking(tripToWarehouse, _content, warehouse, status), Is.True);
            Assert.That(BuildingDecor.IsWorking(courierToWarehouse, _content, warehouse, status), Is.True);
            Assert.That(BuildingDecor.IsWorking(courierToWoodcutter, _content, warehouse, status), Is.False);
        }
    }
}
