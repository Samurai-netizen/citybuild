#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>The arms of a road tile (<see cref="RoadArms"/>): the arms toward roads show, the others hide, and a road alone shows none.</summary>
    public sealed class RoadArmsTests
    {
        /// <summary>The road each test builds.</summary>
        private GameObject? _road;

        /// <summary>Destroys the road.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_road != null)
            {
                Object.DestroyImmediate(_road);
            }
        }

        /// <summary>A road model's four arms.</summary>
        private Transform Road()
        {
            _road = new GameObject("road_l1");
            foreach (string name in new[] { RoadArms.XPlus, RoadArms.XMinus, RoadArms.YPlus, RoadArms.YMinus })
            {
                new GameObject(name).transform.SetParent(_road.transform, false);
            }

            return _road.transform;
        }

        /// <summary>A corner shows the two arms toward its neighbours; alone, a road shows no arm.</summary>
        [Test]
        public void Show_KeepsTheArmsTowardNeighbouringRoads()
        {
            Transform road = Road();
            Assert.That(RoadArms.Show(road, true, false, true, false), Is.EqualTo(4));
            Assert.That(road.Find(RoadArms.XPlus).gameObject.activeSelf, Is.True);
            Assert.That(road.Find(RoadArms.XMinus).gameObject.activeSelf, Is.False);
            Assert.That(road.Find(RoadArms.YPlus).gameObject.activeSelf, Is.True);
            Assert.That(road.Find(RoadArms.YMinus).gameObject.activeSelf, Is.False);
            RoadArms.Show(road, false, false, false, false);
            Assert.That(road.Find(RoadArms.XPlus).gameObject.activeSelf, Is.False);
        }

        /// <summary>The road models are found by level and variant under <c>Models/common</c>.</summary>
        [Test]
        public void PathOf_FollowsTheArtsNames()
        {
            Assert.That(RoadArms.PathOf(2), Is.EqualTo("Models/common/road_l2"));
            Assert.That(RoadArms.PathOf(1, 1), Is.EqualTo("Models/common/road_l1_b"));
            Assert.That(RoadArms.PathOf(2, 2), Is.EqualTo("Models/common/road_l2_c"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RoadArms.PathOf(1, RoadArms.Variants));
        }

        /// <summary>The road variants the art's previews use for the same tiles (<c>road_variant</c>).</summary>
        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(0, 1, 1)]
        [TestCase(5, 2, 2)]
        [TestCase(1, 7, 1)]
        public void VariantFor_MatchesTheArtPreviews(int x, int y, int expected)
        {
            Assert.That(RoadArms.VariantFor(x, y), Is.EqualTo(expected));
        }
    }
}
