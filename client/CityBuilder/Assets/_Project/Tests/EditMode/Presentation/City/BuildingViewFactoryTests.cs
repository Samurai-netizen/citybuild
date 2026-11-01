#nullable enable

using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>The building factory (M0-44, CR-0005-T3): the objects it makes from the models (or, as a fallback, primitives), where they stand, and what a refresh changes.</summary>
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

        /// <summary>A building is an instance of its model, stands in the middle of its tile, has no collider and no primitive look.</summary>
        [Test]
        public void Create_ForATypeWithAModel_InstantiatesTheModel_OnTheBuildingsTile_WithNoCollider()
        {
            BuildingView view = _factory.Create(Idle(7, BuildingLooks.Woodcutter, 3, 2, 1), 1, _parent.transform);

            Assert.That(view.BuildingId, Is.EqualTo(7));
            Assert.That(view.TypeId, Is.EqualTo(BuildingLooks.Woodcutter));
            Assert.That(view.Model, Is.SameAs(BuildingLooks.FindModel(BuildingLooks.Woodcutter, 1, 1)));
            Assert.That(view.Look, Is.Null);
            Assert.That(view.PartCount, Is.EqualTo(0));
            Assert.That(view.Body!.parent, Is.SameAs(view.transform));
            Assert.That(view.Body.name, Is.EqualTo(BuildingViewFactory.BodyName));
            Assert.That(view.Body.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
            Assert.That(view.transform.parent, Is.SameAs(_parent.transform));
            Assert.That(view.transform.localPosition, Is.EqualTo(new Vector3(3.5f, 0f, 2.5f)));
            Assert.That(view.GetComponentsInChildren<Collider>().Length, Is.EqualTo(0));
        }

        /// <summary>The view finds the anchors of its model by name; a model without a chimney has no smoke anchor.</summary>
        [Test]
        public void Create_FindsTheModelsAnchorsByName()
        {
            BuildingView sawmill = _factory.Create(Idle(1, BuildingLooks.Sawmill, 0, 0, 1), 1, _parent.transform);
            BuildingView tradingPost = _factory.Create(Idle(2, BuildingLooks.TradingPost, 1, 0, 1), 1, _parent.transform);

            Assert.That(sawmill.StatusAnchor, Is.Not.Null);
            Assert.That(sawmill.StatusAnchor!.name, Is.EqualTo(BuildingView.StatusAnchorName));
            Assert.That(sawmill.DoorAnchor, Is.Not.Null);
            Assert.That(sawmill.StockpileAnchor, Is.Not.Null);
            Assert.That(sawmill.Wheel, Is.Not.Null);
            Assert.That(sawmill.StatusAnchor.IsChildOf(sawmill.Body), Is.True, "the anchors belong to this instance, not to the asset");
            Assert.That(tradingPost.StatusAnchor, Is.Not.Null);
            Assert.That(tradingPost.Wheel, Is.Null);
        }

        /// <summary>The four types look different: each gets the model of its own type.</summary>
        [Test]
        public void Create_ForEachType_GivesItsOwnModel()
        {
            string[] types = { BuildingLooks.House, BuildingLooks.Woodcutter, BuildingLooks.Sawmill, BuildingLooks.TradingPost };
            var models = new System.Collections.Generic.HashSet<GameObject?>();
            for (int i = 0; i < types.Length; i++)
            {
                BuildingView view = _factory.Create(Idle(i + 1, types[i], i, 0, 1), 1, _parent.transform);
                Assert.That(view.Model, Is.SameAs(BuildingLooks.FindModel(types[i], 1, 1)), types[i]);
                Assert.That(models.Add(view.Model), Is.True, "distinct: " + types[i]);
            }
        }

        /// <summary>A level 2 building is its level 2 model at full scale, not the level 1 model stretched.</summary>
        [Test]
        public void Create_AtLevel2_UsesTheLevel2Model_NotAStretchedLevel1()
        {
            BuildingView level1 = _factory.Create(Idle(1, BuildingLooks.House, 0, 0, 1), 1, _parent.transform);
            BuildingView level2 = _factory.Create(Idle(2, BuildingLooks.House, 1, 0, 2), 1, _parent.transform);

            Assert.That(level2.Model, Is.SameAs(BuildingLooks.FindModel(BuildingLooks.House, 1, 2)));
            Assert.That(level2.Model, Is.Not.SameAs(level1.Model));
            Assert.That(level1.Body!.localScale, Is.EqualTo(Vector3.one));
            Assert.That(level2.Body!.localScale, Is.EqualTo(Vector3.one));
        }

        /// <summary>A building under construction shows the construction site model, not the finished building.</summary>
        [Test]
        public void Create_WhileConstructing_ShowsTheConstructionSite()
        {
            BuildingView view = _factory.Create(Constructing(1, BuildingLooks.Sawmill, 0, 0), 1, _parent.transform);

            Assert.That(view.Phase, Is.EqualTo(BuildingPhase.Constructing));
            Assert.That(view.Model, Is.SameAs(BuildingLooks.FindConstructionSite(1)));
            Assert.That(view.StatusAnchor, Is.Not.Null);
            Assert.That(view.Wheel, Is.Null, "the finished sawmill's wheel is not there yet");
        }

        /// <summary>An upgrading building also shows the construction site; the new level shows only when the server says it is built.</summary>
        [Test]
        public void Refresh_WhileUpgrading_ShowsTheConstructionSite_ThenTheNextLevel()
        {
            BuildingView view = _factory.Create(Idle(1, BuildingLooks.House, 0, 0, 1), 1, _parent.transform);

            Assert.That(_factory.Refresh(view, Upgrading(1, BuildingLooks.House, 0, 0, 1), 1), Is.True);
            Assert.That(view.Model, Is.SameAs(BuildingLooks.FindConstructionSite(1)));
            Assert.That(view.Level, Is.EqualTo(1));

            Assert.That(_factory.Refresh(view, Idle(1, BuildingLooks.House, 0, 0, 2), 1), Is.True);
            Assert.That(view.Model, Is.SameAs(BuildingLooks.FindModel(BuildingLooks.House, 1, 2)));
            Assert.That(view.Level, Is.EqualTo(2));
        }

        /// <summary>A refresh with the same building changes nothing; a new phase, level, era or type builds the body it needs and no old one is left.</summary>
        [Test]
        public void Refresh_ChangesOnlyWhatChanged()
        {
            BuildingView view = _factory.Create(Constructing(1, BuildingLooks.House, 0, 0), 1, _parent.transform);
            Transform site = view.Body!;

            Assert.That(_factory.Refresh(view, Constructing(1, BuildingLooks.House, 0, 0), 1), Is.False, "same building");
            Assert.That(view.Body, Is.SameAs(site), "the same body");

            Assert.That(_factory.Refresh(view, Idle(1, BuildingLooks.House, 0, 0, 1), 1), Is.True, "finished");
            Assert.That(view.Model, Is.SameAs(BuildingLooks.FindModel(BuildingLooks.House, 1, 1)));
            Assert.That(view.Body, Is.Not.SameAs(site), "the site is replaced by the house");
            Assert.That(view.transform.childCount, Is.EqualTo(1), "no old body left");

            Assert.That(_factory.Refresh(view, Idle(1, BuildingLooks.House, 0, 0, 1), 1), Is.False, "same building again");

            Assert.That(_factory.Refresh(view, Idle(1, BuildingLooks.House, 0, 0, 1), 2), Is.True, "a new era");
            Assert.That(view.Era, Is.EqualTo(2));
            Assert.That(view.Model, Is.SameAs(BuildingLooks.FindModel(BuildingLooks.House, 1, 1)), "era 2 has no art of its own, so it keeps the Era I model");

            Assert.That(_factory.Refresh(view, Idle(1, BuildingLooks.Sawmill, 0, 0, 1), 2), Is.True, "a new type");
            Assert.That(view.Model, Is.SameAs(BuildingLooks.FindModel(BuildingLooks.Sawmill, 1, 1)));
            Assert.That(view.transform.childCount, Is.EqualTo(1));
        }

        /// <summary>A level the art has no model for is drawn from the type's primitives, taller by 20 % for each level above 1.</summary>
        [Test]
        public void Create_ForALevelWithoutAModel_BuildsTheTypesPrimitives_TwentyPercentTallerPerLevel()
        {
            BuildingView view = _factory.Create(Idle(1, BuildingLooks.House, 0, 0, 3), 1, _parent.transform);

            Assert.That(view.Model, Is.Null);
            Assert.That(view.Look, Is.SameAs(BuildingLooks.Get(BuildingLooks.House, 1)));
            Assert.That(view.PartCount, Is.EqualTo(BuildingLooks.Get(BuildingLooks.House, 1).Parts.Count));
            Assert.That(view.Body!.localScale.y, Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(view.GetComponentsInChildren<Collider>().Length, Is.EqualTo(0));
            Assert.That(view.StatusAnchor, Is.Null);
        }

        /// <summary>A building of a type the art and the table don't know is drawn as the fallback box.</summary>
        [Test]
        public void Create_ForAnUnknownType_UsesTheFallbackLook()
        {
            BuildingView view = _factory.Create(Idle(1, "castle", 0, 0, 1), 1, _parent.transform);

            Assert.That(view.Model, Is.Null);
            Assert.That(view.Look, Is.SameAs(BuildingLooks.Fallback));
            Assert.That(view.PartCount, Is.EqualTo(BuildingLooks.Fallback.Parts.Count));
        }

        /// <summary>Models of any type and level draw with the art's one shared material.</summary>
        [Test]
        public void Create_ForModels_SharesTheArtsOneMaterial()
        {
            var shared = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Palette_Era1.mat");
            string[] types = { BuildingLooks.House, BuildingLooks.Woodcutter, BuildingLooks.Sawmill, BuildingLooks.TradingPost };
            for (int i = 0; i < types.Length; i++)
            {
                BuildingView view = _factory.Create(Idle(i + 1, types[i], i, 0, 2), 1, _parent.transform);
                foreach (Renderer renderer in view.Body!.GetComponentsInChildren<Renderer>())
                {
                    Assert.That(renderer.sharedMaterial, Is.SameAs(shared), types[i]);
                }
            }

            Assert.That(shared, Is.Not.Null);
        }

        /// <summary>Two primitive buildings that use the same colour share one material.</summary>
        [Test]
        public void Create_ForTwoPrimitiveBuildings_SharesMaterials()
        {
            BuildingView first = _factory.Create(Idle(1, "castle", 0, 0, 1), 1, _parent.transform);
            BuildingView second = _factory.Create(Idle(2, "castle", 1, 0, 1), 1, _parent.transform);

            Material a = first.Body!.GetChild(0).GetComponent<Renderer>().sharedMaterial;
            Material b = second.Body!.GetChild(0).GetComponent<Renderer>().sharedMaterial;
            Assert.That(b, Is.SameAs(a));
        }

        /// <summary>An idle building at <paramref name="x"/>, <paramref name="y"/> of the given type and level.</summary>
        private static Building Idle(int id, string typeId, int x, int y, int level)
        {
            return new Building(id, new BuildingTypeId(typeId), x, y, level, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        }

        /// <summary>A building of the given level that is being upgraded at <paramref name="x"/>, <paramref name="y"/>.</summary>
        private static Building Upgrading(int id, string typeId, int x, int y, int level)
        {
            return new Building(id, new BuildingTypeId(typeId), x, y, level, 0, BuildingPhase.Upgrading, 10, 70, level + 1, 0, 0, 0);
        }

        /// <summary>A level 1 building under construction at <paramref name="x"/>, <paramref name="y"/>.</summary>
        private static Building Constructing(int id, string typeId, int x, int y)
        {
            return new Building(id, new BuildingTypeId(typeId), x, y, 1, 0, BuildingPhase.Constructing, 10, 70, 0, 0, 0, 0);
        }
    }
}
