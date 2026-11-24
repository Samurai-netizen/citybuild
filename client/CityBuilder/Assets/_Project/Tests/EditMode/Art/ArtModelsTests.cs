#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Tests.EditMode.Net;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using NUnit.Framework;
using UnityEngine;
using Terrain = CityBuilder.Simulation.Content.Terrain;

namespace CityBuilder.Client.Tests.EditMode.Art
{
    /// <summary>
    /// The models the client draws (CR-0005-T3): every building type of the content has a model for each level it can reach, the construction
    /// site and the ground tiles exist, and each building model carries the anchors the views look for. The models are found the way a player
    /// build finds them, by name from <c>Resources</c>, so a model outside that folder fails here instead of missing from a build.
    /// Visual v2 (CR-0008-T2) adds: every building model has its anchors and only moving parts with poses that the motion table knows; the fill
    /// models have steps 1 to n of known items; the eight grass variants, every variant of the road tiles of each content road level with their arms,
    /// the smoke puff, the walkers and a carry model for every good exist.
    /// </summary>
    public sealed class ArtModelsTests
    {
        /// <summary>The content of a real <c>GET /content</c> answer (<c>Tests/Fixtures/content.json</c>), through the factory the client uses.</summary>
        private static GameContent LoadContent()
        {
            var dto = new ApiJson().Deserialize<ContentResponseDto>(Fixtures.Read("content.json"))!;
            GameContentResult result = GameContentFactory.Create(ContentMapper.ToData(dto));
            Assert.That(result.IsValid, Is.True, "the fixture is valid content");
            return result.Content!;
        }

        /// <summary>The building types of the content, one test case each.</summary>
        private static IEnumerable<string> BuildingTypeIds()
        {
            foreach (BuildingDefinition building in LoadContent().Buildings)
            {
                yield return building.Id.Value;
            }
        }

        /// <summary>
        /// Every level a building type can reach has a model in the type's own era, except the levels on the pending list (<see cref="PendingAssets.Models"/>),
        /// which the client draws as code-made placeholders (<c>27</c> → Placeholders, M1-02 decision 22).
        /// </summary>
        [TestCaseSource(nameof(BuildingTypeIds))]
        public void EveryReachableLevel_HasAModel_InTheTypesEra(string typeId)
        {
            Assert.That(LoadContent().TryGetBuilding(new BuildingTypeId(typeId), out BuildingDefinition? building), Is.True);

            for (int level = 1; level <= building!.MaxLevel; level++)
            {
                if (PendingAssets.Contains(PendingAssets.Models, ModelName(typeId, level)))
                {
                    continue;
                }

                GameObject? model = Resources.Load<GameObject>(BuildingLooks.ModelPath(typeId, building.Era, level));
                Assert.That(model, Is.Not.Null, typeId + " level " + level + " (" + BuildingLooks.ModelPath(typeId, building.Era, level) + ")");
            }
        }

        /// <summary>Every building model has the anchors for the status bubble and for the door (a pending level has no model yet).</summary>
        [TestCaseSource(nameof(BuildingTypeIds))]
        public void EveryBuildingModel_HasTheStatusAndDoorAnchors(string typeId)
        {
            Assert.That(LoadContent().TryGetBuilding(new BuildingTypeId(typeId), out BuildingDefinition? building), Is.True);

            for (int level = 1; level <= building!.MaxLevel; level++)
            {
                if (PendingAssets.Contains(PendingAssets.Models, ModelName(typeId, level)))
                {
                    continue;
                }

                GameObject model = BuildingLooks.FindModel(typeId, building.Era, level)!;
                Assert.That(BuildingViewFactory.FindByName(model.transform, BuildingView.StatusAnchorName), Is.Not.Null, typeId + " level " + level);
                Assert.That(BuildingViewFactory.FindByName(model.transform, BuildingView.DoorAnchorName), Is.Not.Null, typeId + " level " + level);
            }
        }

        /// <summary>
        /// The pending list holds only models the content needs and the art lacks: each is a level its type can reach, and none has a file. A
        /// delivered model must leave the list, so the tests above check it.
        /// </summary>
        [Test]
        public void ThePendingModels_AreReachableLevelsWithoutAModel()
        {
            GameContent content = LoadContent();
            var reachable = new Dictionary<string, string>();
            foreach (BuildingDefinition building in content.Buildings)
            {
                for (int level = 1; level <= building.MaxLevel; level++)
                {
                    reachable[ModelName(building.Id.Value, level)] = BuildingLooks.ModelPath(building.Id.Value, building.Era, level);
                }
            }

            Assert.That(PendingAssets.Models, Is.Unique);
            foreach (string name in PendingAssets.Models)
            {
                Assert.That(reachable.TryGetValue(name, out string? path), Is.True, name + " is a reachable level of the content");
                Assert.That(Resources.Load<GameObject>(path!), Is.Null, name + " has arrived: take it off the pending list");
            }
        }

        /// <summary>The pending carried goods are goods of the content (not the currency) without a <c>carry_</c> model.</summary>
        [Test]
        public void ThePendingCarryModels_AreGoodsWithoutAModel()
        {
            var goods = new HashSet<string>();
            foreach (ItemDefinition item in LoadContent().Items)
            {
                if (!item.IsCurrency)
                {
                    goods.Add(CityWalkers.CarryPrefix + item.Id.Value);
                }
            }

            Assert.That(PendingAssets.CarryModels, Is.Unique);
            foreach (string name in PendingAssets.CarryModels)
            {
                Assert.That(goods.Contains(name), Is.True, name + " is a good of the content");
                Assert.That(Resources.Load<GameObject>(CityWalkers.ModelFolder + name), Is.Null, name + " has arrived: take it off the pending list");
            }
        }

        /// <summary>The name of a building model without its folder: <c>house_l2</c>.</summary>
        private static string ModelName(string typeId, int level) => typeId + "_l" + level;

        /// <summary>The Sawmill, which the view will turn the wheel of, has a <c>Wheel</c> at both levels.</summary>
        [TestCase(1)]
        [TestCase(2)]
        public void TheSawmill_HasAWheel(int level)
        {
            GameObject model = BuildingLooks.FindModel(BuildingLooks.Sawmill, 1, level)!;

            Assert.That(BuildingViewFactory.FindByName(model.transform, BuildingView.WheelName), Is.Not.Null);
        }

        /// <summary>The construction site exists for Era I and has the status and door anchors, like a finished building.</summary>
        [Test]
        public void TheConstructionSite_ExistsForEraI_WithTheAnchors()
        {
            GameObject? site = BuildingLooks.FindConstructionSite(1);

            Assert.That(site, Is.Not.Null);
            Assert.That(BuildingViewFactory.FindByName(site!.transform, BuildingView.StatusAnchorName), Is.Not.Null);
            Assert.That(BuildingViewFactory.FindByName(site.transform, BuildingView.DoorAnchorName), Is.Not.Null);
        }

        /// <summary>The ground tiles exist.</summary>
        [Test]
        public void TheGroundTiles_Exist()
        {
            Assert.That(Resources.Load<GameObject>(BuildingLooks.GrassTilePath), Is.Not.Null, "grass");
            Assert.That(Resources.Load<GameObject>(BuildingLooks.DirtTilePath), Is.Not.Null, "dirt");
        }

        /// <summary>
        /// The map's models of AR-M1-03 exist (M1-03): every model <see cref="GroundLooks"/> picks for every terrain, the bridge, both mist puffs
        /// and the survey stake.
        /// </summary>
        [Test]
        public void TheMapModels_Exist()
        {
            var paths = new SortedSet<string> { GroundLooks.BridgePath, GroundLooks.MistPuffPath, GroundLooks.MistEdgePuffPath, GroundLooks.SurveyStakePath };
            foreach (Terrain terrain in new[] { Terrain.Forest, Terrain.Rock, Terrain.Soil, Terrain.Water })
            {
                for (int x = 0; x < 18; x++)
                {
                    for (int y = 0; y < 14; y++)
                    {
                        paths.Add(GroundLooks.ModelPathFor(terrain, x, y));
                    }
                }
            }

            Assert.That(paths, Has.Count.EqualTo(15));
            foreach (string path in paths)
            {
                Assert.That(Resources.Load<GameObject>(path), Is.Not.Null, path);
            }
        }

        /// <summary>
        /// The terrain tiles sit edge to edge with the grass (AR-M1-03): a land tile's top is at y = 0 and a river's surface at y = -0.05, and
        /// each fills its tile (1 × 1, centred on its pivot).
        /// </summary>
        [Test]
        public void EveryTerrainTile_FillsItsTile_AtTheGroundsHeight()
        {
            foreach (Terrain terrain in new[] { Terrain.Forest, Terrain.Rock, Terrain.Soil, Terrain.Water })
            {
                for (int x = 0; x < 18; x++)
                {
                    string path = GroundLooks.ModelPathFor(terrain, x, 0);
                    Bounds bounds = Resources.Load<GameObject>(path).GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                    Assert.That(bounds.size.x, Is.EqualTo(1f).Within(0.02f), path);
                    Assert.That(bounds.size.z, Is.EqualTo(1f).Within(0.02f), path);
                    Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.02f), path);
                    Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.02f), path);
                    if (terrain == Terrain.Water)
                    {
                        Assert.That(bounds.max.y, Is.EqualTo(-0.05f).Within(0.01f), path);
                    }
                    else
                    {
                        Assert.That(bounds.min.y, Is.LessThan(0f), path + ": the slab reaches below the ground plane like the grass");
                    }
                }
            }
        }

        /// <summary>
        /// The map's models stay inside the triangle budgets of AR-M1-03: 1,000 per terrain tile, 2,000 for the bridge, 300 for a mist puff and the
        /// survey stake.
        /// </summary>
        [Test]
        public void TheMapModels_StayInsideTheirTriangleBudgets()
        {
            var budgets = new Dictionary<string, int>
            {
                [GroundLooks.BridgePath] = 2000,
                [GroundLooks.MistPuffPath] = 300,
                [GroundLooks.MistEdgePuffPath] = 300,
                [GroundLooks.SurveyStakePath] = 300,
            };
            foreach (Terrain terrain in new[] { Terrain.Forest, Terrain.Rock, Terrain.Soil, Terrain.Water })
            {
                for (int x = 0; x < 18; x++)
                {
                    budgets[GroundLooks.ModelPathFor(terrain, x, 0)] = 1000;
                }
            }

            foreach (KeyValuePair<string, int> budget in budgets)
            {
                int triangles = 0;
                foreach (MeshFilter filter in Resources.Load<GameObject>(budget.Key).GetComponentsInChildren<MeshFilter>())
                {
                    triangles += filter.sharedMesh.triangles.Length / 3;
                }

                Assert.That(triangles, Is.InRange(1, budget.Value), budget.Key);
            }
        }

        /// <summary>A later era with no models of its own finds the Era I ones; a type or a level without a model finds none (and is logged once, 27).</summary>
        [Test]
        public void FindModel_ForALaterEra_KeepsTheEarlierModel_AndForAMissingOneIsNull()
        {
            Assert.That(BuildingLooks.FindModel(BuildingLooks.House, 3, 1), Is.SameAs(BuildingLooks.FindModel(BuildingLooks.House, 1, 1)));
            Assert.That(BuildingLooks.FindConstructionSite(3), Is.SameAs(BuildingLooks.FindConstructionSite(1)));
            Assert.That(BuildingLooks.FindModel("castle", 1, 1), Is.Null);
            Assert.That(BuildingLooks.FindModel(BuildingLooks.House, 1, 9), Is.Null);
            Assert.That(BuildingLooks.FindModel(BuildingLooks.House, 0, 1), Is.Null);
        }

        /// <summary>The names of the model paths follow the art's rule <c>Models/era&lt;N&gt;/&lt;typeId&gt;_l&lt;level&gt;</c>.</summary>
        [Test]
        public void ModelPath_FollowsTheArtsNamingRule()
        {
            Assert.That(BuildingLooks.ModelPath("trading_post", 1, 2), Is.EqualTo("Models/era1/trading_post_l2"));
            Assert.That(BuildingLooks.ConstructionSitePath(1), Is.EqualTo("Models/era1/construction_site"));
        }

        /// <summary>A building under construction or upgrade is drawn as the site; a finished or producing one is not.</summary>
        [TestCase(CityBuilder.Simulation.State.BuildingPhase.Constructing, true)]
        [TestCase(CityBuilder.Simulation.State.BuildingPhase.Upgrading, true)]
        [TestCase(CityBuilder.Simulation.State.BuildingPhase.Idle, false)]
        [TestCase(CityBuilder.Simulation.State.BuildingPhase.Producing, false)]
        public void ShowsConstructionSite_IsTrueOnlyWhileBuildingOrUpgrading(CityBuilder.Simulation.State.BuildingPhase phase, bool expected)
        {
            Assert.That(BuildingLooks.ShowsConstructionSite(phase), Is.EqualTo(expected));
        }

        // The models of visual v2 (CR-0008-T2), merged in from the former ArtModelsV2Tests.

        /// <summary>Every building model of visual v2, by its path under <c>Resources</c>.</summary>
        private static readonly string[] BuildingModels =
        {
            "Models/era1/house_l1", "Models/era1/house_l2", "Models/era1/woodcutter_l1", "Models/era1/woodcutter_l2",
            "Models/era1/sawmill_l1", "Models/era1/sawmill_l2", "Models/era1/trading_post_l1", "Models/era1/trading_post_l2",
            "Models/era1/construction_site", "Models/era1/scriptorium_l1", "Models/era1/courier_station_l1",
            "Models/era1/courier_station_l2", "Models/era1/warehouse_l1", "Models/era1/warehouse_l2",
        };

        /// <summary>Every fill model and the items it must have steps for.</summary>
        private static readonly Dictionary<string, string[]> FillModels = new Dictionary<string, string[]>
        {
            ["Models/era1/woodcutter_l1_fill"] = new[] { "logs" }, ["Models/era1/woodcutter_l2_fill"] = new[] { "logs" },
            ["Models/era1/sawmill_l1_fill"] = new[] { "logs", "planks" }, ["Models/era1/sawmill_l2_fill"] = new[] { "logs", "planks" },
            ["Models/era1/trading_post_l1_fill"] = new[] { "planks" }, ["Models/era1/trading_post_l2_fill"] = new[] { "planks" },
            ["Models/era1/warehouse_l1_fill"] = new[] { "logs", "planks" }, ["Models/era1/warehouse_l2_fill"] = new[] { "logs", "planks" },
        };

        /// <summary>The building model paths, one test case each.</summary>
        private static IEnumerable<string> Buildings() => BuildingModels;

        /// <summary>The fill model paths, one test case each.</summary>
        private static IEnumerable<string> Fills() => FillModels.Keys;

        /// <summary>A building model exists with the status and door anchors, and its moving parts (if any) form a rig the table knows.</summary>
        [TestCaseSource(nameof(Buildings))]
        public void EveryBuildingModel_HasItsAnchors_AndKnownMovingParts(string path)
        {
            GameObject? model = Resources.Load<GameObject>(path);
            Assert.That(model, Is.Not.Null, path);
            Assert.That(BuildingViewFactory.FindByName(model!.transform, BuildingView.StatusAnchorName), Is.Not.Null, path);
            Assert.That(BuildingViewFactory.FindByName(model.transform, BuildingView.DoorAnchorName), Is.Not.Null, path);
            int poses = 0;
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.EndsWith(PartMotions.PoseSuffix, System.StringComparison.Ordinal))
                {
                    poses++;
                    string part = child.name.Substring(0, child.name.Length - PartMotions.PoseSuffix.Length);
                    Assert.That(PartMotions.TryGet(part, out _), Is.True, path + ": " + part);
                }
            }

            MotionRig? rig = MotionRig.Find(model.transform);
            Assert.That(rig == null ? 0 : rig.Count, Is.EqualTo(poses), path + ": every pose has its part");
        }

        /// <summary>The producers move: the Woodcutter's axe, the Sawmill's wheel and saw, the Trading Post's coin sign and flag.</summary>
        [TestCase("Models/era1/woodcutter_l1", "Axe")]
        [TestCase("Models/era1/sawmill_l1", "Wheel")]
        [TestCase("Models/era1/sawmill_l2", "Saw")]
        [TestCase("Models/era1/trading_post_l1", "Sign")]
        [TestCase("Models/era1/trading_post_l2", "Flag_2")]
        [TestCase("Models/era1/construction_site", "Hammer")]
        public void TheProducers_HaveTheirMovingParts(string path, string part)
        {
            GameObject model = Resources.Load<GameObject>(path);
            Assert.That(BuildingViewFactory.FindByName(model.transform, part), Is.Not.Null, path);
            Assert.That(BuildingViewFactory.FindByName(model.transform, part + PartMotions.PoseSuffix), Is.Not.Null, path);
        }

        /// <summary>A fill model has, for each of its items, steps 1 to n with n from 1 to 3, and nothing else.</summary>
        [TestCaseSource(nameof(Fills))]
        public void EveryFillModel_HasStepsOneToN(string path)
        {
            GameObject? model = Resources.Load<GameObject>(path);
            Assert.That(model, Is.Not.Null, path);
            var fill = new FillView(Object.Instantiate(model!).transform);
            try
            {
                var items = new List<string>();
                for (int i = 0; i < fill.ItemCount; i++)
                {
                    items.Add(fill.ItemAt(i).Value);
                    Assert.That(fill.StepCountAt(i), Is.InRange(1, StorageFill.MaxSteps), path);
                }

                Assert.That(items, Is.EquivalentTo(FillModels[path]), path);
                Assert.That(fill.Root.childCount, Is.EqualTo(CountSteps(fill)), path + ": only step objects");
            }
            finally
            {
                Object.DestroyImmediate(fill.Root.gameObject);
            }
        }

        /// <summary>The ground: the eight grass variants, the dirt tile, every variant of both road levels with its four arms, and the smoke puff.</summary>
        [Test]
        public void TheGround_TheRoads_AndTheSmoke_Exist()
        {
            for (int v = 0; v < GroundVariants.Count; v++)
            {
                Assert.That(Resources.Load<GameObject>(GroundVariants.PathOf(v)), Is.Not.Null, GroundVariants.PathOf(v));
            }

            Assert.That(Resources.Load<GameObject>(BuildingLooks.DirtTilePath), Is.Not.Null);
            for (int level = 1; level <= 2; level++)
            {
                for (int variant = 0; variant < RoadArms.Variants; variant++)
                {
                    string path = RoadArms.PathOf(level, variant);
                    GameObject road = Resources.Load<GameObject>(path);
                    Assert.That(road, Is.Not.Null, path);
                    foreach (string arm in new[] { RoadArms.XPlus, RoadArms.XMinus, RoadArms.YPlus, RoadArms.YMinus })
                    {
                        Assert.That(BuildingViewFactory.FindByName(road.transform, arm), Is.Not.Null, path + ": " + arm);
                    }
                }
            }

            Assert.That(Resources.Load<GameObject>(BuildingDecor.SmokePuffPath), Is.Not.Null);
        }

        /// <summary>
        /// Every grass tile is flat ground with its top at y = 0 (the renderer GroundTiles tints for placement hints, and what buildings stand
        /// on, as GroundTilesTests checks) and a <c>Details</c> child with everything that rises above it.
        /// </summary>
        [Test]
        public void EveryGrassTile_IsFlatGround_WithItsDetailsAsAChild()
        {
            for (int v = 0; v < GroundVariants.Count; v++)
            {
                string path = GroundVariants.PathOf(v);
                GameObject tile = Resources.Load<GameObject>(path);
                Assert.That(tile, Is.Not.Null, path);
                Mesh ground = tile.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(ground.bounds.max.y, Is.EqualTo(0f).Within(0.01f), path);
                Assert.That(tile.transform.Find("Details"), Is.Not.Null, path);
            }
        }

        /// <summary>The number of step objects of a fill view.</summary>
        private static int CountSteps(FillView fill)
        {
            int count = 0;
            for (int i = 0; i < fill.ItemCount; i++)
            {
                count += fill.StepCountAt(i);
            }

            return count;
        }

        /// <summary>Every road level of the content has all its tile variants (CR-0008): a level the content adds without art fails here.</summary>
        [Test]
        public void EveryRoadLevelOfTheContent_HasItsTileModels()
        {
            foreach (RoadLevelDefinition level in LoadContent().RoadLevels)
            {
                for (int variant = 0; variant < RoadArms.Variants; variant++)
                {
                    Assert.That(Resources.Load<GameObject>(RoadArms.PathOf(level.Level, variant)), Is.Not.Null, "road level " + level.Level + " variant " + variant);
                }
            }
        }

        /// <summary>
        /// The walkers (CR-0008-T2 part D): the courier and the worker exist with moving legs and arms, the courier has the <c>Anchor_Load</c> its load sits on,
        /// and every good except the currency has a <c>carry_&lt;item&gt;</c> model for it, except the pending ones (<see cref="PendingAssets.CarryModels"/>).
        /// </summary>
        [Test]
        public void TheWalkers_AndEveryCarriedGood_HaveModels()
        {
            GameObject? courier = Resources.Load<GameObject>(CityWalkers.ModelFolder + CityWalkers.CourierModelName);
            GameObject? worker = Resources.Load<GameObject>(CityWalkers.ModelFolder + CityWalkers.WorkerModelName);
            Assert.That(courier, Is.Not.Null);
            Assert.That(worker, Is.Not.Null);
            Assert.That(MotionRig.Find(courier!.transform), Is.Not.Null, "the courier walks with arms and legs");
            Assert.That(MotionRig.Find(worker!.transform), Is.Not.Null, "the worker walks with arms and legs");
            Assert.That(BuildingViewFactory.FindByName(courier.transform, WalkerView.LoadAnchorName), Is.Not.Null);
            foreach (ItemDefinition item in LoadContent().Items)
            {
                if (!item.IsCurrency && !PendingAssets.Contains(PendingAssets.CarryModels, CityWalkers.CarryPrefix + item.Id.Value))
                {
                    Assert.That(Resources.Load<GameObject>(CityWalkers.ModelFolder + CityWalkers.CarryPrefix + item.Id.Value), Is.Not.Null, "carry model of " + item.Id.Value);
                }
            }
        }
    }
}
