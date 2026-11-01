#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The building views of the city in a running scene (M0-44): given a sample city, the right number and types of views stand on the right
    /// tiles; a second city updates the views that changed and removes the ones that are gone, with no duplicates. Objects are destroyed at the
    /// end of the frame, so the checks that count children wait a frame.
    /// </summary>
    public sealed class CityViewTests
    {
        /// <summary>The object that holds the world under test.</summary>
        private GameObject _host = null!;

        /// <summary>The world under test, showing an 8 by 8 grid.</summary>
        private CityWorld _world = null!;

        /// <summary>Before each test: creates a world with an 8 by 8 grid.</summary>
        [SetUp]
        public void CreateWorld()
        {
            _host = new GameObject("Test world host");
            _world = _host.AddComponent<CityWorld>();
            _world.ShowGrid(new CityGrid(8, 8));
        }

        /// <summary>After each test: destroys the world.</summary>
        [TearDown]
        public void DestroyWorld()
        {
            Object.DestroyImmediate(_host);
        }

        /// <summary>Before any city is shown there are no buildings.</summary>
        [Test]
        public void BeforeACityIsShown_NoBuildingsStand()
        {
            Assert.That(_world.Buildings, Is.Not.Null);
            Assert.That(_world.Buildings!.Count, Is.EqualTo(0));
        }

        /// <summary>A sample city of four buildings gets four views of the right types on the right tiles.</summary>
        [Test]
        public void ShowCity_GivesOneViewPerBuilding_WithTheRightTypeAndTile()
        {
            _world.ShowCity(City(FirstSample()));

            CityView buildings = _world.Buildings!;
            Assert.That(buildings.Count, Is.EqualTo(4));
            Assert.That(buildings.Root.transform.childCount, Is.EqualTo(4));
            AssertView(buildings, 1, BuildingLooks.House, new Vector3(2.5f, 0f, 2.5f));
            AssertView(buildings, 2, BuildingLooks.Woodcutter, new Vector3(3.5f, 0f, 2.5f));
            AssertView(buildings, 3, BuildingLooks.Sawmill, new Vector3(4.5f, 0f, 2.5f));
            AssertView(buildings, 4, BuildingLooks.TradingPost, new Vector3(5.5f, 0f, 3.5f));
        }

        /// <summary>Showing the same city again changes nothing: the same view objects stay, and no duplicate appears.</summary>
        [UnityTest]
        public IEnumerator ShowCity_WithTheSameCityAgain_KeepsTheSameViews()
        {
            _world.ShowCity(City(FirstSample()));
            _world.Buildings!.TryGetView(2, out BuildingView? before);

            _world.ShowCity(City(FirstSample()));
            yield return null;

            _world.Buildings.TryGetView(2, out BuildingView? after);
            Assert.That(after, Is.SameAs(before));
            Assert.That(_world.Buildings.Count, Is.EqualTo(4));
            Assert.That(_world.Buildings.Root.transform.childCount, Is.EqualTo(4));
        }

        /// <summary>
        /// The view runs once a second with the display city: a call where nothing changed allocates nothing (M0-44; the models of CR-0005 must not
        /// change that), for finished buildings and for one under construction.
        /// </summary>
        [Test]
        public void ShowCity_WithNothingChanged_AllocatesNothing()
        {
            CityState city = City(FirstSample().Concat(new[] { new Building(5, new BuildingTypeId(BuildingLooks.House), 0, 0, 1, 0, BuildingPhase.Constructing, 0, 30, 0, 0, 0, 0) }));
            _world.ShowCity(city);

            Assert.That(() => _world.Buildings!.Show(city), Is.Not.AllocatingGCMemory());
        }

        /// <summary>All the buildings of a city draw with the art's one material, so a city is a handful of draw calls and not one per building.</summary>
        [Test]
        public void ShowCity_DrawsEveryBuildingWithTheOneSharedMaterial()
        {
            _world.ShowCity(City(FirstSample().Concat(new[] { Idle(5, BuildingLooks.House, 0, 0, 2) })));

            var materials = new HashSet<Material>();
            foreach (Renderer renderer in _world.Buildings!.Root.GetComponentsInChildren<Renderer>())
            {
                materials.Add(renderer.sharedMaterial);
            }

            Assert.That(materials.Count, Is.EqualTo(1));
        }

        /// <summary>A second city updates the changed building in place, adds the new one and removes the missing ones, with no duplicates.</summary>
        [UnityTest]
        public IEnumerator ShowCity_WithASecondCity_UpdatesAddsAndRemoves()
        {
            _world.ShowCity(City(FirstSample()));
            _world.Buildings!.TryGetView(1, out BuildingView? cottage);
            _world.Buildings.TryGetView(2, out BuildingView? hut);

            // The cottage is upgraded to level 2, the hut and the trading post are gone, the sawmill stays, a second cottage is new.
            _world.ShowCity(City(new[]
            {
                Idle(1, BuildingLooks.House, 2, 2, 2),
                Idle(3, BuildingLooks.Sawmill, 4, 2, 1),
                Idle(5, BuildingLooks.House, 0, 7, 1),
            }));
            yield return null;

            CityView buildings = _world.Buildings;
            Assert.That(buildings.Count, Is.EqualTo(3));
            Assert.That(buildings.Root.transform.childCount, Is.EqualTo(3), "no duplicate and no leftover object");
            Assert.That(buildings.TryGetView(1, out BuildingView? updated), Is.True);
            Assert.That(updated, Is.SameAs(cottage), "the same object, updated in place");
            Assert.That(updated!.Level, Is.EqualTo(2));
            Assert.That(updated.Model, Is.SameAs(BuildingLooks.FindModel(BuildingLooks.House, 1, 2)), "the level 2 model");
            Assert.That(buildings.TryGetView(2, out _), Is.False, "the hut is gone");
            Assert.That(hut == null, Is.True, "its object is destroyed");
            Assert.That(buildings.TryGetView(4, out _), Is.False, "the trading post is gone");
            AssertView(buildings, 3, BuildingLooks.Sawmill, new Vector3(4.5f, 0f, 2.5f));
            AssertView(buildings, 5, BuildingLooks.House, new Vector3(0.5f, 0f, 7.5f));
        }

        /// <summary>An empty city removes every view.</summary>
        [UnityTest]
        public IEnumerator ShowCity_WithAnEmptyCity_RemovesEveryView()
        {
            _world.ShowCity(City(FirstSample()));

            _world.ShowCity(City(Array.Empty<Building>()));
            yield return null;

            Assert.That(_world.Buildings!.Count, Is.EqualTo(0));
            Assert.That(_world.Buildings.Root.transform.childCount, Is.EqualTo(0));
        }

        /// <summary>A building the server is still constructing shows as the construction site, and as the finished building once it is idle.</summary>
        [Test]
        public void ShowCity_ShowsAConstructingBuildingAsTheSite_ThenFinished()
        {
            _world.ShowCity(City(new[] { new Building(1, new BuildingTypeId(BuildingLooks.House), 1, 1, 1, 0, BuildingPhase.Constructing, 0, 30, 0, 0, 0, 0) }));
            _world.Buildings!.TryGetView(1, out BuildingView? view);
            Assert.That(view!.Model, Is.SameAs(BuildingLooks.FindConstructionSite(1)));

            _world.ShowCity(City(new[] { Idle(1, BuildingLooks.House, 1, 1, 1) }));

            Assert.That(view.Model, Is.SameAs(BuildingLooks.FindModel(BuildingLooks.House, 1, 1)));
            Assert.That(_world.Buildings.Count, Is.EqualTo(1));
        }

        /// <summary>Showing the grid again (a new city size) replaces the building views with an empty set.</summary>
        [UnityTest]
        public IEnumerator ShowGrid_Again_ReplacesTheBuildingViews()
        {
            _world.ShowCity(City(FirstSample()));
            GameObject oldRoot = _world.Buildings!.Root;

            _world.ShowGrid(new CityGrid(4, 4));
            yield return null;

            Assert.That(oldRoot == null, Is.True);
            Assert.That(_world.Buildings!.Count, Is.EqualTo(0));
        }

        /// <summary>The first sample city's buildings: a cottage, a hut, a sawmill and a trading post.</summary>
        private static Building[] FirstSample()
        {
            return new[]
            {
                Idle(1, BuildingLooks.House, 2, 2, 1),
                Idle(2, BuildingLooks.Woodcutter, 3, 2, 1),
                Idle(3, BuildingLooks.Sawmill, 4, 2, 1),
                Idle(4, BuildingLooks.TradingPost, 5, 3, 1),
            };
        }

        /// <summary>A city of Era I with the given buildings.</summary>
        private static CityState City(IEnumerable<Building> buildings)
        {
            return new CityState(0, 1, 10, new List<KeyValuePair<ItemId, long>>(), buildings, new List<ResearchId>(), null);
        }

        /// <summary>An idle building of the given type, tile and level.</summary>
        private static Building Idle(int id, string typeId, int x, int y, int level)
        {
            return new Building(id, new BuildingTypeId(typeId), x, y, level, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        }

        /// <summary>Asserts that building <paramref name="id"/> has a view of the given type standing at <paramref name="position"/>.</summary>
        private static void AssertView(CityView buildings, int id, string typeId, Vector3 position)
        {
            Assert.That(buildings.TryGetView(id, out BuildingView? view), Is.True, "view of building " + id);
            Assert.That(view!.TypeId, Is.EqualTo(typeId));
            Assert.That(view.Model, Is.SameAs(BuildingLooks.FindModel(typeId, 1, 1)));
            Assert.That(view.transform.localPosition, Is.EqualTo(position));
        }
    }
}
