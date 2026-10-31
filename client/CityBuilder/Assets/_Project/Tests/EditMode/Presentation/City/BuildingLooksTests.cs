#nullable enable

using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>The placeholder looks and their lookup by type and era (M0-44), and the height rule: the maths behind the building views, with no scene.</summary>
    public sealed class BuildingLooksTests
    {
        /// <summary>Every building type of the M0 content has its own look in Era I.</summary>
        [TestCase(BuildingLooks.House, "cottage")]
        [TestCase(BuildingLooks.Woodcutter, "woodcutters-hut")]
        [TestCase(BuildingLooks.Sawmill, "sawmill")]
        [TestCase(BuildingLooks.TradingPost, "trading-post")]
        public void Get_ForEveryM0Type_ReturnsItsOwnEraILook(string typeId, string name)
        {
            Assert.That(BuildingLooks.Has(typeId, 1), Is.True);
            Assert.That(BuildingLooks.Get(typeId, 1).Name, Is.EqualTo(name));
        }

        /// <summary>The four Era I looks are four different objects with parts to draw.</summary>
        [Test]
        public void Looks_AreFourDistinctOnesWithParts()
        {
            Assert.That(BuildingLooks.Count, Is.EqualTo(4));
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (string typeId in new[] { BuildingLooks.House, BuildingLooks.Woodcutter, BuildingLooks.Sawmill, BuildingLooks.TradingPost })
            {
                BuildingLook look = BuildingLooks.Get(typeId, 1);
                Assert.That(look.Parts.Count, Is.GreaterThanOrEqualTo(2), typeId);
                Assert.That(names.Add(look.Name), Is.True, "distinct: " + typeId);
            }
        }

        /// <summary>A later era without its own look keeps the latest earlier one.</summary>
        [Test]
        public void Get_ForALaterEraWithNoLook_KeepsTheEarlierLook()
        {
            Assert.That(BuildingLooks.Has(BuildingLooks.House, 3), Is.False);
            Assert.That(BuildingLooks.Get(BuildingLooks.House, 3), Is.SameAs(BuildingLooks.Get(BuildingLooks.House, 1)));
        }

        /// <summary>A type the table doesn't know is drawn as the fallback box, in any era.</summary>
        [TestCase("castle", 1)]
        [TestCase("castle", 4)]
        [TestCase("house", 0)]
        public void Get_ForAnUnknownTypeOrEra_ReturnsTheFallback(string typeId, int era)
        {
            Assert.That(BuildingLooks.Get(typeId, era), Is.SameAs(BuildingLooks.Fallback));
        }

        /// <summary>Every part's middle lies over its one-tile footprint (a tile is a unit wide) and above the ground.</summary>
        [Test]
        public void EveryPart_HasItsMiddleOverTheTile_AndAboveTheGround()
        {
            foreach (string typeId in new[] { BuildingLooks.House, BuildingLooks.Woodcutter, BuildingLooks.Sawmill, BuildingLooks.TradingPost })
            {
                foreach (BuildingPart part in BuildingLooks.Get(typeId, 1).Parts)
                {
                    Assert.That(part.Center.y, Is.GreaterThan(0f), typeId);
                    Assert.That(Mathf.Abs(part.Center.x), Is.LessThan(0.5f), typeId);
                    Assert.That(Mathf.Abs(part.Center.z), Is.LessThan(0.5f), typeId);
                }
            }
        }

        /// <summary>Level 1 is full height, level 2 is 20 % taller, and each further level adds another 20 % of the level 1 height.</summary>
        [TestCase(1, 1000)]
        [TestCase(2, 1200)]
        [TestCase(3, 1400)]
        public void HeightPermille_RisesTwentyPercentPerLevel(int level, int expected)
        {
            Assert.That(BuildingLooks.HeightPermille(level, BuildingPhase.Idle), Is.EqualTo(expected));
            Assert.That(BuildingLooks.HeightPermille(level, BuildingPhase.Producing), Is.EqualTo(expected));
        }

        /// <summary>An upgrading building still shows its current level; the new level shows only when the server says it is built.</summary>
        [Test]
        public void HeightPermille_WhileUpgrading_IsTheCurrentLevels()
        {
            Assert.That(BuildingLooks.HeightPermille(1, BuildingPhase.Upgrading), Is.EqualTo(1000));
        }

        /// <summary>A building under construction is a low foundation whatever its level.</summary>
        [Test]
        public void HeightPermille_WhileConstructing_IsALowFoundation()
        {
            Assert.That(BuildingLooks.HeightPermille(1, BuildingPhase.Constructing), Is.EqualTo(BuildingLooks.ConstructionHeightPermille));
            Assert.That(BuildingLooks.ConstructionHeightPermille, Is.LessThan(BuildingLooks.FullHeightPermille));
        }

        /// <summary>A look needs a name and at least one part.</summary>
        [Test]
        public void Look_WithoutANameOrParts_IsRefused()
        {
            Assert.Throws<System.ArgumentException>(() => new BuildingLook("", new[] { BuildingPart.Box(Vector3.zero, Vector3.one, Color.white) }));
            Assert.Throws<System.ArgumentException>(() => new BuildingLook("empty", System.Array.Empty<BuildingPart>()));
            Assert.Throws<System.ArgumentNullException>(() => new BuildingLook("null", null!));
        }
    }
}
