#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation.Content;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// <see cref="GroundLooks"/> (M1-03): each tile of the map gets the model of its terrain and one of that terrain's variants, by the tile's
    /// fixed hash, so the same map always looks the same; grass keeps the eight grass variants of visual v2.
    /// </summary>
    public sealed class GroundLooksTests
    {
        /// <summary>A grass tile shows the grass variant and the quarter turn it showed before the map (<see cref="GroundVariants"/>).</summary>
        [Test]
        public void ModelPathFor_Grass_IsTheGrassVariantOfTheTile()
        {
            for (int x = 0; x < 18; x++)
            {
                for (int y = 0; y < 14; y++)
                {
                    Assert.That(GroundLooks.ModelPathFor(Terrain.Grass, x, y), Is.EqualTo(GroundVariants.PathOf(GroundVariants.For(x, y))));
                    Assert.That(GroundLooks.QuarterTurnsFor(Terrain.Grass, x, y), Is.EqualTo(GroundVariants.RotationFor(x, y)));
                }
            }
        }

        /// <summary>
        /// Forest, rock and soil tiles show one of their three models (<c>tile_forest</c>, <c>_b</c>, <c>_c</c>) and water one of its two; over
        /// the 18 × 14 map every variant of every terrain is used.
        /// </summary>
        [Test]
        public void ModelPathFor_EachTerrain_UsesEveryVariantOfItsOwnModels()
        {
            var expected = new Dictionary<Terrain, string[]>
            {
                [Terrain.Forest] = new[] { "Models/common/tile_forest", "Models/common/tile_forest_b", "Models/common/tile_forest_c" },
                [Terrain.Rock] = new[] { "Models/common/tile_rock", "Models/common/tile_rock_b", "Models/common/tile_rock_c" },
                [Terrain.Soil] = new[] { "Models/common/tile_soil", "Models/common/tile_soil_b", "Models/common/tile_soil_c" },
                [Terrain.Water] = new[] { "Models/common/tile_water", "Models/common/tile_water_b" },
            };
            foreach (KeyValuePair<Terrain, string[]> terrain in expected)
            {
                var seen = new HashSet<string>();
                for (int x = 0; x < 18; x++)
                {
                    for (int y = 0; y < 14; y++)
                    {
                        string path = GroundLooks.ModelPathFor(terrain.Key, x, y);
                        Assert.That(terrain.Value, Does.Contain(path), terrain.Key + " at " + x + "," + y);
                        seen.Add(path);
                    }
                }

                Assert.That(seen, Is.EquivalentTo(terrain.Value), terrain.Key.ToString());
            }
        }

        /// <summary>The look is fixed: the same terrain and tile give the same model and turn every time.</summary>
        [Test]
        public void ModelPathFor_SameTile_IsAlwaysTheSame()
        {
            Assert.That(GroundLooks.ModelPathFor(Terrain.Forest, 3, 7), Is.EqualTo(GroundLooks.ModelPathFor(Terrain.Forest, 3, 7)));
            Assert.That(GroundLooks.QuarterTurnsFor(Terrain.Rock, 3, 7), Is.EqualTo(GroundLooks.QuarterTurnsFor(Terrain.Rock, 3, 7)));
        }

        /// <summary>
        /// Soil and water turn only by half turns, so furrows and the river keep their direction; forest and rock turn by any quarter turn, and
        /// over the map they use all four.
        /// </summary>
        [Test]
        public void QuarterTurnsFor_SoilAndWater_AreHalfTurns_ForestAndRockAnyTurn()
        {
            var forestTurns = new HashSet<int>();
            var rockTurns = new HashSet<int>();
            for (int x = 0; x < 18; x++)
            {
                for (int y = 0; y < 14; y++)
                {
                    Assert.That(GroundLooks.QuarterTurnsFor(Terrain.Soil, x, y), Is.EqualTo(0).Or.EqualTo(2));
                    Assert.That(GroundLooks.QuarterTurnsFor(Terrain.Water, x, y), Is.EqualTo(0).Or.EqualTo(2));
                    forestTurns.Add(GroundLooks.QuarterTurnsFor(Terrain.Forest, x, y));
                    rockTurns.Add(GroundLooks.QuarterTurnsFor(Terrain.Rock, x, y));
                }
            }

            Assert.That(forestTurns, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
            Assert.That(rockTurns, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
        }

        /// <summary>Each terrain has its own placeholder tint (the flat tile shown while its model is missing), and none is the grass green.</summary>
        [Test]
        public void PlaceholderColorFor_EachTerrain_IsItsOwn()
        {
            var colors = new HashSet<UnityEngine.Color>();
            foreach (Terrain terrain in new[] { Terrain.Grass, Terrain.Forest, Terrain.Rock, Terrain.Soil, Terrain.Water })
            {
                colors.Add(GroundLooks.PlaceholderColorFor(terrain));
            }

            Assert.That(colors, Has.Count.EqualTo(5));
        }
    }
}
