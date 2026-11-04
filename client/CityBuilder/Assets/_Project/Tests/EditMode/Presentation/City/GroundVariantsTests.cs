#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// The grass variant of each tile (<see cref="GroundVariants"/>): the same values as the art previews' <c>ground_variant</c> (the vectors
    /// were computed there), and a mix close to 4 : 2 : 2 : 2 over a large map.
    /// </summary>
    public sealed class GroundVariantsTests
    {
        /// <summary>The values the art's previews use for the same tiles.</summary>
        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 0)]
        [TestCase(0, 1, 1)]
        [TestCase(3, 5, 3)]
        [TestCase(7, 7, 1)]
        [TestCase(2, 6, 0)]
        [TestCase(5, 2, 2)]
        [TestCase(6, 1, 1)]
        public void TheVariant_MatchesTheArtPreviews(int x, int y, int expected)
        {
            Assert.That(GroundVariants.For(x, y), Is.EqualTo(expected));
        }

        /// <summary>Over 100 x 100 tiles the plain tile shows on about 40 % and each other variant on about 20 %.</summary>
        [Test]
        public void OverALargeMap_TheMixIsFourTwoTwoTwo()
        {
            var counts = new int[GroundVariants.Count];
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    counts[GroundVariants.For(x, y)]++;
                }
            }

            Assert.That(counts[0], Is.InRange(3700, 4300));
            for (int v = 1; v < GroundVariants.Count; v++)
            {
                Assert.That(counts[v], Is.InRange(1800, 2200), "variant " + v);
            }
        }

        /// <summary>The paths follow the art's names under <c>Models/common</c>.</summary>
        [Test]
        public void ThePaths_FollowTheArtsNames()
        {
            Assert.That(GroundVariants.PathOf(0), Is.EqualTo("Models/common/tile_grass"));
            Assert.That(GroundVariants.PathOf(3), Is.EqualTo("Models/common/tile_grass_d"));
        }
    }
}
