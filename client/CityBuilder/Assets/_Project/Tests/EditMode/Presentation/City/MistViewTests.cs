#nullable enable

using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Tests.EditMode.Core;
using CityBuilder.Client.Tests.EditMode.Presentation.Build;
using CityBuilder.Simulation.Content;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// The mist over land research has not opened (M1-03, AR-M1-03): every closed tile has mist, the tiles beside open land the flatter edge
    /// puffs and a survey stake, open land none; when an area opens its mist lifts over <see cref="MistView.RevealSeconds"/> and is then gone.
    /// </summary>
    public sealed class MistViewTests
    {
        /// <summary>The real content with its map.</summary>
        private GameContent _content = null!;

        /// <summary>The object the view hangs under.</summary>
        private GameObject _parent = null!;

        /// <summary>The view under test.</summary>
        private MistView _mist = null!;

        /// <summary>Before each test: the mist of the start city (only area A open).</summary>
        [SetUp]
        public void CreateMist()
        {
            _content = BuildMenuTestData.Content();
            _parent = new GameObject("Mist parent");
            _mist = new MistView(new CityGrid(_content.Rules.GridWidth, _content.Rules.GridHeight), _parent.transform);
            _mist.Show(BuildMenuTestData.StartCity(), _content);
        }

        /// <summary>After each test: destroys the view and its parent.</summary>
        [TearDown]
        public void Cleanup()
        {
            _mist.Destroy();
            Object.DestroyImmediate(_parent);
        }

        /// <summary>Every tile of areas B and C has mist, no tile of area A has: 18 × 14 tiles, 110 of them open.</summary>
        [Test]
        public void Show_TheStartCity_PutsMistOnEveryClosedTile()
        {
            Assert.That(_mist.MistTileCount, Is.EqualTo(18 * 14 - 110));
            Assert.That(_mist.HasMistAt(new GridCoordinate(0, 0)), Is.True, "area B");
            Assert.That(_mist.HasMistAt(new GridCoordinate(15, 5)), Is.True, "area C");
            Assert.That(_mist.HasMistAt(new GridCoordinate(5, 5)), Is.False, "area A");
        }

        /// <summary>A closed tile beside open land is an edge tile (flatter puffs and a survey stake); a closed tile with closed land all round is not.</summary>
        [Test]
        public void Show_TheTilesBesideOpenLand_AreTheEdge()
        {
            Assert.That(_mist.IsEdgeAt(new GridCoordinate(1, 5)), Is.True, "beside (2, 5)");
            Assert.That(_mist.IsEdgeAt(new GridCoordinate(13, 5)), Is.True, "beside (12, 5)");
            Assert.That(_mist.IsEdgeAt(new GridCoordinate(0, 5)), Is.False);
            Assert.That(_mist.IsEdgeAt(new GridCoordinate(5, 5)), Is.False, "open land has no mist");
            Assert.That(_mist.Root.transform.Find("Mist (1, 5)").Find(MistView.StakeName), Is.Not.Null);
            Assert.That(_mist.Root.transform.Find("Mist (0, 5)").Find(MistView.StakeName), Is.Null);
        }

        /// <summary>
        /// When area C opens its mist lifts: the tiles are revealing until <see cref="MistView.RevealSeconds"/> have passed, then their mist is
        /// gone; area B keeps its mist, and the tiles beside the new open land become the edge.
        /// </summary>
        [Test]
        public void Show_AfterAnAreaOpens_ItsMistLiftsAndIsGone()
        {
            int before = _mist.MistTileCount;

            _mist.Show(BuildMenuTestData.City(800, 0, 0, new[] { "open_fields" }, 0), _content);

            Assert.That(_mist.RevealingTileCount, Is.EqualTo(5 * 14));
            Assert.That(_mist.HasMistAt(new GridCoordinate(15, 5)), Is.False, "no longer counted as mist");
            _mist.Tick(MistView.RevealSeconds * 0.5f);
            Assert.That(_mist.RevealingTileCount, Is.EqualTo(5 * 14), "still lifting");
            _mist.Tick(MistView.RevealSeconds);
            Assert.That(_mist.RevealingTileCount, Is.Zero);
            Assert.That(_mist.Root.transform.Find("Mist (15, 5)"), Is.Null);
            Assert.That(_mist.MistTileCount, Is.EqualTo(before - 5 * 14));
            Assert.That(_mist.HasMistAt(new GridCoordinate(0, 0)), Is.True);
        }

        /// <summary>A city shown again with the same open land changes nothing and makes no new objects.</summary>
        [Test]
        public void Show_TheSameLandAgain_ChangesNothing()
        {
            int children = _mist.Root.transform.childCount;

            _mist.Show(BuildMenuTestData.StartCity(), _content);

            Assert.That(_mist.Root.transform.childCount, Is.EqualTo(children));
            Assert.That(_mist.RevealingTileCount, Is.Zero);
        }

        /// <summary>Content without a map has no mist at all.</summary>
        [Test]
        public void Show_ContentWithoutAMap_HasNoMist()
        {
            GameContent content = CityTestData.SnapshotsContent();
            var parent = new GameObject("No map parent");
            var mist = new MistView(new CityGrid(content.Rules.GridWidth, content.Rules.GridHeight), parent.transform);
            try
            {
                mist.Show(BuildMenuTestData.StartCity(), content);

                Assert.That((mist.MistTileCount, mist.Root.transform.childCount), Is.EqualTo((0, 0)));
            }
            finally
            {
                mist.Destroy();
                Object.DestroyImmediate(parent);
            }
        }
    }
}
