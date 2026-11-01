#nullable enable

using System.Collections.Generic;
using System.IO;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The washed-out look of a stalled building in a running scene (M0-50): every renderer of its body gets a property block (the art's shared
    /// material is not copied or changed), the block goes when the building runs again, and a new body of a still-stalled building gets it again.
    /// </summary>
    public sealed class StalledTintTests
    {
        /// <summary>The object that holds the world under test.</summary>
        private GameObject _host = null!;

        /// <summary>The world under test, showing an 8 by 8 grid.</summary>
        private CityWorld _world = null!;

        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: loads the content and makes a world with an 8 by 8 grid.</summary>
        [SetUp]
        public void CreateWorld()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures/content.json"));
            ContentResponseDto dto = new ApiJson().Deserialize<ContentResponseDto>(json)!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(dto));
            Assert.That(built.IsValid, Is.True);
            _content = built.Content!;
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

        /// <summary>A woodcutter without workers is stalled: each renderer of its model gets a darker base colour and a grey emission, and the shared material stays as it was.</summary>
        [Test]
        public void AStalledBuilding_GetsAPropertyBlockOnEveryRenderer_AndTheSharedMaterialIsUntouched()
        {
            Show(Woodcutter(0, BuildingPhase.Idle));

            BuildingView view = ViewOf(1);
            Assert.That(view.Stalled, Is.True);
            Renderer[] renderers = view.Body!.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.GreaterThan(0));
            float kept = 1f - StalledTint.Mix;
            foreach (Renderer renderer in renderers)
            {
                Assert.That(renderer.HasPropertyBlock(), Is.True, renderer.name);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(new Color(kept, kept, kept, 1f)).Using(ColorEqualityComparer.Instance));
                float lift = StalledTint.Grey * StalledTint.Mix;
                Assert.That(block.GetColor("_EmissionColor"), Is.EqualTo(new Color(lift, lift, lift, 1f)).Using(ColorEqualityComparer.Instance));
                Assert.That(renderer.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(Color.white).Using(ColorEqualityComparer.Instance), "the shared material is not changed");
            }
        }

        /// <summary>The shared material has its emission keyword on, so the grey the block adds is drawn, with an emission too dark to change a normal building.</summary>
        [Test]
        public void TheSharedMaterial_HasEmissionOn_WithAnInvisibleEmission()
        {
            Show(Woodcutter(0, BuildingPhase.Idle));

            Material material = ViewOf(1).Body!.GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(material.GetColor("_EmissionColor").maxColorComponent, Is.LessThan(1f / 255f));
        }

        /// <summary>When the workers come back the property blocks are removed and the building looks normal again.</summary>
        [Test]
        public void WhenTheBuildingRunsAgain_TheBlocksGo()
        {
            Show(Woodcutter(0, BuildingPhase.Idle));

            Show(Woodcutter(2, BuildingPhase.Idle));

            BuildingView view = ViewOf(1);
            Assert.That(view.Stalled, Is.False);
            foreach (Renderer renderer in view.Body!.GetComponentsInChildren<Renderer>())
            {
                Assert.That(renderer.HasPropertyBlock(), Is.False, renderer.name);
            }
        }

        /// <summary>A building that is not stalled never gets a block, and is not marked stalled.</summary>
        [Test]
        public void ABuildingThatRuns_IsNotTinted()
        {
            Show(Woodcutter(2, BuildingPhase.Idle));

            BuildingView view = ViewOf(1);
            Assert.That(view.Stalled, Is.False);
            Assert.That(view.Body!.GetComponentInChildren<Renderer>().HasPropertyBlock(), Is.False);
        }

        /// <summary>A stalled building whose body is replaced (it is rebuilt as the construction site, then the model again) gets the look on the new body.</summary>
        [Test]
        public void ANewBody_OfAStalledBuilding_GetsTheLookAgain()
        {
            Show(Woodcutter(0, BuildingPhase.Idle));
            Transform first = ViewOf(1).Body!;

            Show(new Building(1, new BuildingTypeId("woodcutter"), 1, 1, 1, 0, BuildingPhase.Upgrading, 0, 100, 2, 0, 0, 0));
            BuildingView view = ViewOf(1);
            Assert.That(view.Body, Is.Not.SameAs(first), "the construction site is a new body");
            Assert.That(view.Stalled, Is.False, "an upgrading building is not stalled");
            Assert.That(view.Body!.GetComponentInChildren<Renderer>().HasPropertyBlock(), Is.False);

            Show(Woodcutter(0, BuildingPhase.Idle));

            Assert.That(view.Stalled, Is.True);
            foreach (Renderer renderer in view.Body!.GetComponentsInChildren<Renderer>())
            {
                Assert.That(renderer.HasPropertyBlock(), Is.True, renderer.name);
            }
        }

        /// <summary>The status bubble, pips and ring are not part of the body, so the look never touches them.</summary>
        [Test]
        public void TheStatusObjects_AreNotTinted()
        {
            Show(Woodcutter(0, BuildingPhase.Idle));

            foreach (Renderer renderer in ViewOf(1).Status!.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.HasPropertyBlock(), Is.False, renderer.name);
            }
        }

        /// <summary>The view of building <paramref name="id"/>.</summary>
        private BuildingView ViewOf(int id)
        {
            Assert.That(_world.Buildings!.TryGetView(id, out BuildingView? view), Is.True);
            return view!;
        }

        /// <summary>Shows a city with 40 logs and the given buildings, with the content.</summary>
        private void Show(params Building[] buildings)
        {
            var stock = new List<KeyValuePair<ItemId, long>>
            {
                new KeyValuePair<ItemId, long>(new ItemId("coins"), 700),
                new KeyValuePair<ItemId, long>(new ItemId("logs"), 40),
                new KeyValuePair<ItemId, long>(new ItemId("planks"), 10),
            };
            _world.ShowCity(new CityState(0, 1, 100, stock, new List<Building>(buildings), new List<ResearchId>(), null), _content);
        }

        /// <summary>The woodcutter, building 1 on tile (1, 1), in <paramref name="phase"/> with <paramref name="workers"/> workers (idle only).</summary>
        private static Building Woodcutter(int workers, BuildingPhase phase)
        {
            return new Building(1, new BuildingTypeId("woodcutter"), 1, 1, 1, workers, phase, 0, null, 0, 0, 0, 0);
        }
    }
}
