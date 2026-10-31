#nullable enable

using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>The building factory (M0-44): the objects it makes from primitives, where they stand, and what a refresh changes.</summary>
    public sealed class BuildingViewFactoryTests
    {
        /// <summary>An 8 by 8 grid, the size of the M0 content.</summary>
        private readonly CityGrid _grid = new CityGrid(8, 8);

        /// <summary>The object the views hang under.</summary>
        private GameObject _parent = null!;

        /// <summary>The factory under test.</summary>
        private BuildingViewFactory _factory = null!;

        /// <summary>Before each test: a fresh parent and factory.</summary>
        [SetUp]
        public void CreateFactory()
        {
            _parent = new GameObject("Test parent");
            _factory = new BuildingViewFactory(_grid);
        }

        /// <summary>After each test: destroys the views and the materials.</summary>
        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_parent);
            _factory.Destroy();
        }

        /// <summary>A building is made from its look's primitives, stands in the middle of its tile and has no collider.</summary>
        [Test]
        public void Create_BuildsTheLooksPrimitives_OnTheBuildingsTile_WithNoCollider()
        {
            BuildingView view = _factory.Create(Idle(7, BuildingLooks.Woodcutter, 3, 2, 1), 1, _parent.transform);

            Assert.That(view.BuildingId, Is.EqualTo(7));
            Assert.That(view.TypeId, Is.EqualTo(BuildingLooks.Woodcutter));
            Assert.That(view.PartCount, Is.EqualTo(BuildingLooks.Get(BuildingLooks.Woodcutter, 1).Parts.Count));
            Assert.That(view.transform.parent, Is.SameAs(_parent.transform));
            Assert.That(view.transform.localPosition, Is.EqualTo(new Vector3(3.5f, 0f, 2.5f)));
            Assert.That(view.GetComponentsInChildren<Collider>().Length, Is.EqualTo(0));
        }

        /// <summary>The four types look different: their primitives differ in number or in colour.</summary>
        [Test]
        public void Create_ForEachType_GivesItsOwnLook()
        {
            string[] types = { BuildingLooks.House, BuildingLooks.Woodcutter, BuildingLooks.Sawmill, BuildingLooks.TradingPost };
            for (int i = 0; i < types.Length; i++)
            {
                BuildingView view = _factory.Create(Idle(i + 1, types[i], i, 0, 1), 1, _parent.transform);
                Assert.That(view.Look, Is.SameAs(BuildingLooks.Get(types[i], 1)), types[i]);
            }
        }

        /// <summary>A level 2 building is 20 % taller than a level 1 one.</summary>
        [Test]
        public void Create_AtLevel2_IsTwentyPercentTaller()
        {
            BuildingView level1 = _factory.Create(Idle(1, BuildingLooks.House, 0, 0, 1), 1, _parent.transform);
            BuildingView level2 = _factory.Create(Idle(2, BuildingLooks.House, 1, 0, 2), 1, _parent.transform);

            Assert.That(level1.Body!.localScale.y, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(level2.Body!.localScale.y, Is.EqualTo(1.2f).Within(0.0001f));
        }

        /// <summary>A building under construction is drawn as a low foundation, not as the finished building.</summary>
        [Test]
        public void Create_WhileConstructing_IsLowerThanAFinishedBuilding()
        {
            BuildingView view = _factory.Create(Constructing(1, BuildingLooks.Sawmill, 0, 0), 1, _parent.transform);

            Assert.That(view.Phase, Is.EqualTo(BuildingPhase.Constructing));
            Assert.That(view.Body!.localScale.y, Is.LessThan(0.5f));
        }

        /// <summary>A refresh with the same building changes nothing; a new level or phase changes the height only; a new era or type builds a new body.</summary>
        [Test]
        public void Refresh_ChangesOnlyWhatChanged()
        {
            BuildingView view = _factory.Create(Constructing(1, BuildingLooks.House, 0, 0), 1, _parent.transform);
            Transform body = view.Body!;

            Assert.That(_factory.Refresh(view, Constructing(1, BuildingLooks.House, 0, 0), 1), Is.False, "same building");

            Assert.That(_factory.Refresh(view, Idle(1, BuildingLooks.House, 0, 0, 1), 1), Is.True, "finished");
            Assert.That(view.Body, Is.SameAs(body), "the same body");
            Assert.That(view.Body!.localScale.y, Is.EqualTo(1f).Within(0.0001f));

            Assert.That(_factory.Refresh(view, Idle(1, BuildingLooks.House, 0, 0, 2), 1), Is.True, "upgraded");
            Assert.That(view.Level, Is.EqualTo(2));
            Assert.That(view.Body!.localScale.y, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(view.Body, Is.SameAs(body), "still the same body");

            Assert.That(_factory.Refresh(view, Idle(1, BuildingLooks.House, 0, 0, 2), 2), Is.True, "a new era");
            Assert.That(view.Era, Is.EqualTo(2));
            Assert.That(view.Body, Is.Not.SameAs(body), "a new body");
            Assert.That(view.PartCount, Is.EqualTo(BuildingLooks.Get(BuildingLooks.House, 2).Parts.Count), "no old parts left");
        }

        /// <summary>A building of a type with no look is drawn as the fallback box.</summary>
        [Test]
        public void Create_ForAnUnknownType_UsesTheFallbackLook()
        {
            BuildingView view = _factory.Create(Idle(1, "castle", 0, 0, 1), 1, _parent.transform);

            Assert.That(view.Look, Is.SameAs(BuildingLooks.Fallback));
            Assert.That(view.PartCount, Is.EqualTo(BuildingLooks.Fallback.Parts.Count));
        }

        /// <summary>Two buildings that use the same colour share one material.</summary>
        [Test]
        public void Create_ForTwoBuildingsOfOneType_SharesMaterials()
        {
            BuildingView first = _factory.Create(Idle(1, BuildingLooks.House, 0, 0, 1), 1, _parent.transform);
            BuildingView second = _factory.Create(Idle(2, BuildingLooks.House, 1, 0, 1), 1, _parent.transform);

            Material a = first.Body!.GetChild(0).GetComponent<Renderer>().sharedMaterial;
            Material b = second.Body!.GetChild(0).GetComponent<Renderer>().sharedMaterial;
            Assert.That(b, Is.SameAs(a));
        }

        /// <summary>An idle building at <paramref name="x"/>, <paramref name="y"/> of the given type and level.</summary>
        private static Building Idle(int id, string typeId, int x, int y, int level)
        {
            return new Building(id, new BuildingTypeId(typeId), x, y, level, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        }

        /// <summary>A level 1 building under construction at <paramref name="x"/>, <paramref name="y"/>.</summary>
        private static Building Constructing(int id, string typeId, int x, int y)
        {
            return new Building(id, new BuildingTypeId(typeId), x, y, 1, 0, BuildingPhase.Constructing, 10, 70, 0, 0, 0, 0);
        }
    }
}
