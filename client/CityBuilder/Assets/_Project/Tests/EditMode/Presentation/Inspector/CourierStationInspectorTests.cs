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
    /// The courier station section of the building inspector (CR-0008-T2 part C): one line per courier with its state, stamina and route
    /// summary, and the Edit routes button that shows only on a built station with couriers.
    /// </summary>
    public sealed class CourierStationInspectorTests
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

        /// <summary>A courier station (id 1) with two couriers shows each by slot with its state, stamina and route, or that it has none.</summary>
        [Test]
        public void Station_ListsEachCourierBySlotWithStateStaminaAndRoute()
        {
            Building station = Station(workers: 2, BuildingPhase.Idle);
            Building sawmill = new Building(2, new BuildingTypeId("sawmill"), 1, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            Building house = HudTestData.House(3, 2);
            var route = new Route(1, 1, new[]
            {
                new RouteStop(2, new[] { new RouteAction(RouteActionKind.Take, new ItemId("logs"), Amount.Preset(new AmountPresetId("all"))) }),
                new RouteStop(3, new[] { new RouteAction(RouteActionKind.Put, new ItemId("logs"), Amount.Preset(new AmountPresetId("all"))) }),
            });
            var couriers = new[]
            {
                new Courier(1, 2, CourierState.Resting, 0, null, null, 0),
                new Courier(1, 1, CourierState.Walking, 2, 0, 30, 600),
            };

            BuildingInspectorModel model = Model(station, new[] { station, sawmill, house }, couriers, new[] { route });

            string[] lines = model.CouriersText.Split('\n');
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0], Does.StartWith("Courier 1 · Working · Stamina "));
            Assert.That(lines[0], Does.Contain("Route: " + NameOf("sawmill") + " → " + NameOf("house")));
            Assert.That(lines[1], Does.StartWith("Courier 2 · Resting · Stamina "));
            Assert.That(lines[1], Does.EndWith("No route"));
            Assert.That(model.HasRouteEditor, Is.True);
        }

        /// <summary>A station with no workers has no couriers: the text says to assign workers and Edit routes stays hidden.</summary>
        [Test]
        public void Station_WithoutCouriers_SaysSoAndHidesTheEditor()
        {
            Building station = Station(workers: 0, BuildingPhase.Idle);

            BuildingInspectorModel model = Model(station, new[] { station }, Array.Empty<Courier>(), Array.Empty<Route>());

            Assert.That(model.CouriersText, Does.Contain("No couriers"));
            Assert.That(model.HasRouteEditor, Is.False);
        }

        /// <summary>A station under construction has nothing to show: no courier lines and no editor.</summary>
        [Test]
        public void Station_UnderConstruction_ShowsNoCouriersAndNoEditor()
        {
            // The engine allows no workers on a construction site, so the empty text comes from the constructing guard alone.
            Building station = Station(workers: 0, BuildingPhase.Constructing);

            BuildingInspectorModel model = Model(station, new[] { station }, Array.Empty<Courier>(), Array.Empty<Route>());

            Assert.That(model.CouriersText, Is.Empty);
            Assert.That(model.HasRouteEditor, Is.False);
        }

        /// <summary>Another kind of building has no courier section.</summary>
        [Test]
        public void OtherBuilding_HasNoCourierSection()
        {
            Building sawmill = new Building(1, new BuildingTypeId("sawmill"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);

            BuildingInspectorModel model = Model(sawmill, new[] { sawmill }, Array.Empty<Courier>(), Array.Empty<Route>());

            Assert.That(model.CouriersText, Is.Empty);
            Assert.That(model.HasRouteEditor, Is.False);
        }

        /// <summary>The same courier lines make equal models, so the view has nothing to redraw; a changed stamina makes them differ.</summary>
        [Test]
        public void Equality_FollowsTheCourierLines()
        {
            Building station = Station(workers: 1, BuildingPhase.Idle);
            var rested = new[] { new Courier(1, 1, CourierState.AtStation, 0, null, null, 600) };
            var tired = new[] { new Courier(1, 1, CourierState.AtStation, 0, null, null, 300) };

            BuildingInspectorModel first = Model(station, new[] { station }, rested, Array.Empty<Route>());
            BuildingInspectorModel same = Model(station, new[] { station }, rested, Array.Empty<Route>());
            BuildingInspectorModel other = Model(station, new[] { station }, tired, Array.Empty<Route>());

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(other));
        }

        /// <summary>Re-energize shows for a station with a resting courier or one below full stamina, and not for rested couriers or other buildings.</summary>
        [Test]
        public void Station_OffersReenergizeOnlyWhenACourierRestsOrIsBelowFullStamina()
        {
            Building station = Station(workers: 1, BuildingPhase.Idle);
            Assert.That(_content.TryGetBuilding(station.TypeId, out BuildingDefinition? definition), Is.True);
            int full = (int)definition!.Levels[0].StaminaSeconds;
            var rested = new[] { new Courier(1, 1, CourierState.AtStation, 0, null, null, full) };
            var resting = new[] { new Courier(1, 1, CourierState.Resting, 0, null, null, 0) };
            var below = new[] { new Courier(1, 1, CourierState.AtStation, 0, null, null, full - 1) };

            Assert.That(Model(station, new[] { station }, rested, Array.Empty<Route>()).HasReenergize, Is.False);
            Assert.That(Model(station, new[] { station }, resting, Array.Empty<Route>()).HasReenergize, Is.True);
            Assert.That(Model(station, new[] { station }, below, Array.Empty<Route>()).HasReenergize, Is.True);
        }

        /// <summary>A courier station with id 1 at level 1.</summary>
        private static Building Station(int workers, BuildingPhase phase)
        {
            return new Building(1, new BuildingTypeId("courier_station"), 0, 0, 1, workers, phase, 0, phase == BuildingPhase.Constructing ? 100 : (long?)null, 0, 0, 0, 0);
        }

        /// <summary>The display name of the building type <paramref name="typeId"/> in the content.</summary>
        private string NameOf(string typeId)
        {
            Assert.That(_content.TryGetBuilding(new BuildingTypeId(typeId), out BuildingDefinition? definition), Is.True);
            return definition!.Name;
        }

        /// <summary>The model of <paramref name="building"/> in a city of <paramref name="buildings"/> with the couriers and routes.</summary>
        private BuildingInspectorModel Model(Building building, Building[] buildings, Courier[] couriers, Route[] routes)
        {
            var city = new CityState(
                0, 1, 10, 700, buildings, Array.Empty<ResearchId>(), null, null, null, 1, couriers, routes);
            BuildingInspectorModel? model = BuildingInspectorModel.Create(_content, city, building, Nothing, default);
            Assert.That(model, Is.Not.Null);
            return model!;
        }
    }
}
