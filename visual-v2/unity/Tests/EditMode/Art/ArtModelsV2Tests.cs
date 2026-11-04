#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Art
{
    /// <summary>
    /// The models of visual v2 (CR-0009) as the client finds them, by name from <c>Resources</c>: every building model has its anchors and only
    /// moving parts with poses that the motion table knows; the fill models have steps 1 to n of known items; the grass variants, the road
    /// tiles with their arms, and the smoke puff exist. CR-0008's buildings are named here because today's content does not have them yet.
    /// </summary>
    public sealed class ArtModelsV2Tests
    {
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

        /// <summary>The ground: four grass variants, the dirt tile, the two road tiles with their four arms, and the smoke puff.</summary>
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
                GameObject road = Resources.Load<GameObject>(RoadArms.PathOf(level));
                Assert.That(road, Is.Not.Null, RoadArms.PathOf(level));
                foreach (string arm in new[] { RoadArms.XPlus, RoadArms.XMinus, RoadArms.YPlus, RoadArms.YMinus })
                {
                    Assert.That(BuildingViewFactory.FindByName(road.transform, arm), Is.Not.Null, arm);
                }
            }

            Assert.That(Resources.Load<GameObject>(BuildingDecor.SmokePuffPath), Is.Not.Null);
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
    }
}
