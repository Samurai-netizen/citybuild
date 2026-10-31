#nullable enable

using System.Collections;
using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The city's camera, ground and pointer actions in a running scene (M0-43): what needs a real <see cref="Camera"/> to project screen points onto
    /// the ground. The maths behind them is tested in EditMode (<c>CityGridTests</c>, <c>CityCameraModelTests</c>).
    /// </summary>
    public sealed class CityWorldTests
    {
        /// <summary>The object that holds the world under test.</summary>
        private GameObject _host = null!;

        /// <summary>The world under test, showing an 8 by 8 grid.</summary>
        private CityWorld _world = null!;

        /// <summary>Before each test: creates a world and shows the M0 content's 8 by 8 grid.</summary>
        [SetUp]
        public void CreateWorld()
        {
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

        /// <summary>The camera exists, is the main camera, and clears the screen to a solid colour (so an earlier screen never shows through).</summary>
        [Test]
        public void Camera_IsTheMainCamera_AndClearsToASolidColour()
        {
            Assert.That(_world.Camera, Is.Not.Null);
            Assert.That(_world.Camera.CompareTag("MainCamera"), Is.True);
            Assert.That(_world.Camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(_world.Camera.enabled, Is.True);
        }

        /// <summary>The ground has a block for each of the 64 tiles.</summary>
        [Test]
        public void Ground_HasOneBlockPerTile()
        {
            Assert.That(_world.Ground, Is.Not.Null);
            Assert.That(_world.Ground!.TileCount, Is.EqualTo(64));
        }

        /// <summary>The camera starts framed on the middle of the grid, looking down at an angle, from above the ground.</summary>
        [Test]
        public void Camera_StartsFramedOnTheMiddle_LookingDownAtAnAngle()
        {
            Transform camera = _world.Camera.transform;

            Assert.That(camera.position.y, Is.GreaterThan(1f));
            Assert.That(camera.eulerAngles.x, Is.EqualTo(CityCameraModel.PitchDegrees).Within(0.01f));
            Assert.That(_world.Rig!.TryGetGroundPoint(ScreenCenter(), out Vector3 point), Is.True);
            Assert.That(point.x, Is.EqualTo(4f).Within(0.01f));
            Assert.That(point.z, Is.EqualTo(4f).Within(0.01f));
        }

        /// <summary>The whole grid is in view at the start: every corner of the grid projects inside the screen.</summary>
        [Test]
        public void Camera_AtTheStart_ShowsTheWholeGrid()
        {
            foreach (Vector3 corner in new[] { new Vector3(0f, 0f, 0f), new Vector3(8f, 0f, 0f), new Vector3(0f, 0f, 8f), new Vector3(8f, 0f, 8f) })
            {
                Vector3 screen = _world.Camera.WorldToScreenPoint(corner);

                Assert.That(screen.z, Is.GreaterThan(0f), "in front of the camera: " + corner);
                Assert.That(screen.x, Is.InRange(0f, _world.Camera.pixelWidth), "inside the width: " + corner);
                Assert.That(screen.y, Is.InRange(0f, _world.Camera.pixelHeight), "inside the height: " + corner);
            }
        }

        /// <summary>A drag keeps the ground point that was under the pointer under it.</summary>
        [Test]
        public void DragBy_KeepsTheGroundPointUnderThePointer()
        {
            _world.Rig!.Zoom(0.5f);
            Vector2 from = ScreenCenter();
            Vector2 to = from + new Vector2(30f, -20f);
            _world.Rig.TryGetGroundPoint(from, out Vector3 grabbed);

            _world.Rig.DragBy(from, to);

            Assert.That(_world.Rig.TryGetGroundPoint(to, out Vector3 under), Is.True);
            Assert.That(under.x, Is.EqualTo(grabbed.x).Within(0.01f));
            Assert.That(under.z, Is.EqualTo(grabbed.z).Within(0.01f));
        }

        /// <summary>A long drag stops at the grid's outline: the camera never looks at a point outside the grid.</summary>
        [Test]
        public void DragBy_StopsAtTheGridOutline()
        {
            for (int i = 0; i < 50; i++)
            {
                _world.Rig!.DragBy(ScreenCenter(), ScreenCenter() + new Vector2(-200f, 0f));
            }

            Vector2 focus = _world.Rig!.Model.Focus;
            Assert.That(focus.x, Is.InRange(0f, 8f));
            Assert.That(focus.y, Is.InRange(0f, 8f));
        }

        /// <summary>Zooming moves the camera along its line of sight and stops at the limits.</summary>
        [Test]
        public void Zoom_MovesTheCamera_AndStopsAtTheLimits()
        {
            Vector3 before = _world.Camera.transform.position;

            _world.Rig!.Zoom(0.5f);
            Assert.That(_world.Camera.transform.position.y, Is.LessThan(before.y));

            _world.Rig.Zoom(0.0001f);
            Assert.That(_world.Rig.Model.Distance, Is.EqualTo(CityCameraModel.NearestDistance));

            _world.Rig.Zoom(10000f);
            Assert.That(_world.Rig.Model.Distance, Is.EqualTo(_world.Rig.Model.FarthestDistance));
        }

        /// <summary>A tap on a tile selects it, a tap on it again clears it, and a tap off the grid clears the selection.</summary>
        [Test]
        public void TapAt_SelectsTheTileUnderThePointer()
        {
            Vector2 onTile = ScreenOf(new Vector3(3.5f, 0f, 2.5f));
            Vector2 offGrid = ScreenOf(new Vector3(20f, 0f, 20f));

            _world.Rig!.TapAt(onTile);
            Assert.That(_world.Selection!.Selected, Is.EqualTo(new GridCoordinate(3, 2)));

            _world.Rig.TapAt(onTile);
            Assert.That(_world.Selection.Selected, Is.Null);

            _world.Rig.TapAt(onTile);
            _world.Rig.TapAt(offGrid);
            Assert.That(_world.Selection.Selected, Is.Null);
        }

        /// <summary>Hovering marks the tile under the pointer, and hovering nothing clears the mark.</summary>
        [Test]
        public void HoverAt_MarksTheTileUnderThePointer()
        {
            _world.Rig!.HoverAt(ScreenOf(new Vector3(6.5f, 0f, 1.5f)));
            Assert.That(_world.Selection!.Hovered, Is.EqualTo(new GridCoordinate(6, 1)));
            Assert.That(_world.Ground!.HoverMarker.activeSelf, Is.True);

            _world.Rig.HoverAt(null);
            Assert.That(_world.Selection.Hovered, Is.Null);
            Assert.That(_world.Ground.HoverMarker.activeSelf, Is.False);
        }

        /// <summary>Showing a new grid replaces the ground; the old tiles are gone after the frame (Unity destroys objects at its end).</summary>
        [UnityTest]
        public IEnumerator ShowGrid_AgainWithAnotherSize_ReplacesTheGround()
        {
            GameObject oldGround = _world.Ground!.Root;

            _world.ShowGrid(new CityGrid(4, 3));
            yield return null;

            Assert.That(oldGround == null, Is.True);
            Assert.That(_world.Ground!.TileCount, Is.EqualTo(12));
        }

        /// <summary>The middle of the camera's view, in pixels.</summary>
        private Vector2 ScreenCenter()
        {
            return new Vector2(_world.Camera.pixelWidth * 0.5f, _world.Camera.pixelHeight * 0.5f);
        }

        /// <summary>Where a world point shows on the screen, in pixels.</summary>
        private Vector2 ScreenOf(Vector3 worldPoint)
        {
            Vector3 screen = _world.Camera.WorldToScreenPoint(worldPoint);
            return new Vector2(screen.x, screen.y);
        }
    }
}
