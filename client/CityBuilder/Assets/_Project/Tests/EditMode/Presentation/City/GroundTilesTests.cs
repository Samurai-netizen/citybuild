#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>The ground tiles and their markers (M0-43): the tiles that are built, where they stand, and how the markers follow the selection.</summary>
    public sealed class GroundTilesTests
    {
        /// <summary>A grid that is not square, so width and height cannot be mixed up unnoticed.</summary>
        private readonly CityGrid _grid = new CityGrid(5, 3);

        /// <summary>The selection the markers follow.</summary>
        private TileSelection _selection = null!;

        /// <summary>The object under test's parent.</summary>
        private GameObject _parent = null!;

        /// <summary>The ground under test.</summary>
        private GroundTiles _ground = null!;

        /// <summary>Before each test: builds a ground over a fresh parent.</summary>
        [SetUp]
        public void BuildGround()
        {
            _selection = new TileSelection(_grid);
            _parent = new GameObject("Test parent");
            _ground = new GroundTiles(_grid, _selection, _parent.transform);
        }

        /// <summary>After each test: destroys the ground and the parent.</summary>
        [TearDown]
        public void Cleanup()
        {
            _ground.Destroy();
            Object.DestroyImmediate(_parent);
        }

        /// <summary>The ground has one block per tile plus the two markers, all under the Ground object, which hangs under the parent.</summary>
        [Test]
        public void Build_MakesOneBlockPerTile_AndTwoMarkers()
        {
            Assert.That(_ground.TileCount, Is.EqualTo(15));
            Assert.That(_ground.Root.transform.parent, Is.SameAs(_parent.transform));
            Assert.That(_ground.Root.transform.childCount, Is.EqualTo(15 + 2));
        }

        /// <summary>Each tile block is centred on its tile and its top is the ground plane.</summary>
        [Test]
        public void TileBlocks_StandOnTheirTiles_WithTheirTopAtTheGround()
        {
            Transform block = _ground.Root.transform.Find("Tile (4, 2)");

            Assert.That(block, Is.Not.Null);
            Assert.That(block.localPosition.x, Is.EqualTo(4.5f).Within(0.0001f));
            Assert.That(block.localPosition.z, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(block.localPosition.y + block.localScale.y * 0.5f, Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>The blocks have no colliders, so physics never answers for them.</summary>
        [Test]
        public void TileBlocks_HaveNoColliders()
        {
            Assert.That(_ground.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        /// <summary>Neighbouring tiles differ in colour, like a chessboard.</summary>
        [Test]
        public void NeighbouringTiles_HaveDifferentColours()
        {
            Color a = _ground.Root.transform.Find("Tile (0, 0)").GetComponent<Renderer>().sharedMaterial.color;
            Color b = _ground.Root.transform.Find("Tile (1, 0)").GetComponent<Renderer>().sharedMaterial.color;
            Color c = _ground.Root.transform.Find("Tile (1, 1)").GetComponent<Renderer>().sharedMaterial.color;

            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(a, Is.EqualTo(c));
        }

        /// <summary>Both markers start hidden.</summary>
        [Test]
        public void Markers_StartHidden()
        {
            Assert.That(_ground.HoverMarker.activeSelf, Is.False);
            Assert.That(_ground.SelectionMarker.activeSelf, Is.False);
        }

        /// <summary>The hover marker sits on the hovered tile and hides when nothing is hovered.</summary>
        [Test]
        public void HoverMarker_FollowsTheHoveredTile()
        {
            _selection.SetHovered(new GridCoordinate(3, 1));

            Assert.That(_ground.HoverMarker.activeSelf, Is.True);
            Assert.That(_ground.HoverMarker.transform.localPosition.x, Is.EqualTo(3.5f).Within(0.0001f));
            Assert.That(_ground.HoverMarker.transform.localPosition.z, Is.EqualTo(1.5f).Within(0.0001f));

            _selection.SetHovered(null);

            Assert.That(_ground.HoverMarker.activeSelf, Is.False);
        }

        /// <summary>The selection marker sits on the tapped tile, above the ground, and the hover marker steps aside on that tile.</summary>
        [Test]
        public void SelectionMarker_FollowsTheTappedTile_AndTheHoverMarkerStepsAside()
        {
            _selection.SetHovered(new GridCoordinate(2, 2));
            _selection.Tap(new GridCoordinate(2, 2));

            Assert.That(_ground.SelectionMarker.activeSelf, Is.True);
            Assert.That(_ground.SelectionMarker.transform.localPosition.x, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(_ground.SelectionMarker.transform.localPosition.y, Is.GreaterThan(0f));
            Assert.That(_ground.HoverMarker.activeSelf, Is.False, "the hover marker would sit under the selection marker");

            _selection.SetHovered(new GridCoordinate(0, 0));

            Assert.That(_ground.HoverMarker.activeSelf, Is.True);
        }

        /// <summary>Destroy removes the ground, and the markers no longer follow the selection.</summary>
        [Test]
        public void Destroy_RemovesTheGround_AndStopsListening()
        {
            GameObject root = _ground.Root;

            _ground.Destroy();
            _ground.Destroy();

            Assert.That(root == null, Is.True);
            Assert.DoesNotThrow(() => _selection.SetHovered(new GridCoordinate(1, 1)));
        }
    }
}
