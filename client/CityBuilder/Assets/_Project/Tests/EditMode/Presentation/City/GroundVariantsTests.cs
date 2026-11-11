#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// The grass variant and the turn of each tile (<see cref="GroundVariants"/>): the same values as the art previews' <c>ground_variant</c> and
    /// <c>ground_turns</c> (the vectors were computed there), the weighted mix of the eight variants and an even spread of the four turns.
    /// </summary>
    public sealed class GroundVariantsTests
    {
        /// <summary>The variants the art's previews use for the same tiles.</summary>
        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 0)]
        [TestCase(0, 1, 1)]
        [TestCase(3, 5, 7)]
        [TestCase(7, 7, 0)]
        [TestCase(2, 6, 4)]
        [TestCase(5, 2, 1)]
        [TestCase(6, 1, 0)]
        [TestCase(4, 4, 5)]
        [TestCase(1, 7, 1)]
        public void TheVariant_MatchesTheArtPreviews(int x, int y, int expected)
        {
            Assert.That(GroundVariants.For(x, y), Is.EqualTo(expected));
        }

        /// <summary>The quarter turns the art's previews use for the same tiles.</summary>
        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(0, 1, 2)]
        [TestCase(7, 7, 3)]
        [TestCase(5, 2, 2)]
        [TestCase(1, 7, 0)]
        public void TheTurn_MatchesTheArtPreviews(int x, int y, int expected)
        {
            Assert.That(GroundVariants.RotationFor(x, y), Is.EqualTo(expected));
        }

        /// <summary>Over 100 x 100 tiles each variant shows on about its weight of 20 (the plain tile 25 %, clover 15 %, the others 10 %).</summary>
        [Test]
        public void OverALargeMap_TheMixFollowsTheWeights()
        {
            var counts = new int[GroundVariants.Count];
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    counts[GroundVariants.For(x, y)]++;
                }
            }

            int[] weights = { 5, 2, 2, 3, 2, 2, 2, 2 };
            for (int v = 0; v < GroundVariants.Count; v++)
            {
                int expected = 10000 * weights[v] / GroundVariants.WeightTotal;
                Assert.That(counts[v], Is.InRange(expected * 85 / 100, expected * 115 / 100), "variant " + v);
            }
        }

        /// <summary>Over 100 x 100 tiles each of the four turns shows on about a quarter of them.</summary>
        [Test]
        public void OverALargeMap_TheTurnsAreEven()
        {
            var counts = new int[4];
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    counts[GroundVariants.RotationFor(x, y)]++;
                }
            }

            for (int r = 0; r < 4; r++)
            {
                Assert.That(counts[r], Is.InRange(2300, 2700), "turn " + r);
            }
        }

        /// <summary>The paths follow the art's names under <c>Models/common</c>.</summary>
        [Test]
        public void ThePaths_FollowTheArtsNames()
        {
            Assert.That(GroundVariants.PathOf(0), Is.EqualTo("Models/common/tile_grass"));
            Assert.That(GroundVariants.PathOf(7), Is.EqualTo("Models/common/tile_grass_h"));
        }
    }
}
