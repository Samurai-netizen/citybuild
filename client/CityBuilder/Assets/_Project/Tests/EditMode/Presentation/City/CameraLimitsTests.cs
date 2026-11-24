#nullable enable

using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Tests.EditMode.Presentation.Build;
using CityBuilder.Simulation.Content;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// The camera limits follow the open land (M1-03): <see cref="CameraLimits"/> gives the tiles of the open areas as one rectangle, and
    /// <see cref="CityCameraModel.SetLimits"/> keeps the focus inside it, frames it, and widens without a jump when an area opens.
    /// </summary>
    public sealed class CameraLimitsTests
    {
        /// <summary>The start city sees only area A: columns 2 to 12, rows 2 to 11.</summary>
        [Test]
        public void Of_TheStartCity_IsTheStartArea()
        {
            GameContent content = BuildMenuTestData.Content();

            RectInt limits = CameraLimits.Of(BuildMenuTestData.StartCity(), content);

            Assert.That((limits.xMin, limits.yMin, limits.xMax, limits.yMax), Is.EqualTo((2, 2, 13, 12)));
        }

        /// <summary>Opening area C (the east bank, every row) widens the limits to the map's east edge and its full height.</summary>
        [Test]
        public void Of_AfterAreaCOpens_ReachesTheEastBank()
        {
            GameContent content = BuildMenuTestData.Content();

            RectInt limits = CameraLimits.Of(BuildMenuTestData.City(800, 0, 0, new[] { "open_fields" }, 0), content);

            Assert.That((limits.xMin, limits.yMin, limits.xMax, limits.yMax), Is.EqualTo((2, 0, 18, 14)));
        }

        /// <summary>Content without a map has every tile open: the limits are the whole grid.</summary>
        [Test]
        public void Of_ContentWithoutAMap_IsTheWholeGrid()
        {
            GameContent content = CityBuilder.Client.Tests.EditMode.Core.CityTestData.SnapshotsContent();

            RectInt limits = CameraLimits.Of(BuildMenuTestData.StartCity(), content);

            Assert.That((limits.xMin, limits.yMin, limits.width, limits.height), Is.EqualTo((0, 0, content.Rules.GridWidth, content.Rules.GridHeight)));
        }

        /// <summary>With limits set, panning stops at the limits' outline, not the grid's.</summary>
        [Test]
        public void SetLimits_PanStopsAtTheLimits()
        {
            var camera = new CityCameraModel(new CityGrid(18, 14));
            camera.SetLimits(new RectInt(2, 2, 11, 10));

            camera.Pan(new Vector2(-100f, -100f));
            Assert.That(camera.Focus, Is.EqualTo(new Vector2(2f, 2f)));
            camera.Pan(new Vector2(500f, 500f));
            Assert.That(camera.Focus, Is.EqualTo(new Vector2(13f, 12f)));
        }

        /// <summary>Framing looks at the middle of the limits from the distance that fits them, nearer than the whole grid's.</summary>
        [Test]
        public void Frame_WithLimits_FitsTheLimits()
        {
            var whole = new CityCameraModel(new CityGrid(18, 14));
            var camera = new CityCameraModel(new CityGrid(18, 14));
            camera.SetLimits(new RectInt(2, 2, 11, 10));

            camera.Frame(1f);

            Assert.That(camera.Focus, Is.EqualTo(new Vector2(7.5f, 7f)));
            Assert.That(camera.FarthestDistance, Is.LessThan(whole.FarthestDistance));
            float seen = camera.FarthestDistance * Mathf.Tan(CityCameraModel.FieldOfViewDegrees * 0.5f * Mathf.Deg2Rad);
            Assert.That(seen, Is.GreaterThan(new Vector2(11f, 10f).magnitude * 0.5f));
        }

        /// <summary>Wider limits (an area opened) keep the focus and the distance where they were, and allow going farther: no jump.</summary>
        [Test]
        public void SetLimits_Wider_KeepsTheViewAndRaisesTheFarthestDistance()
        {
            var camera = new CityCameraModel(new CityGrid(18, 14));
            camera.SetLimits(new RectInt(2, 2, 11, 10));
            camera.Frame(1f);
            camera.Pan(new Vector2(1f, 1f));
            Vector2 focus = camera.Focus;
            float distance = camera.Distance;
            float farthest = camera.FarthestDistance;

            camera.SetLimits(new RectInt(2, 0, 16, 14));

            Assert.That((camera.Focus, camera.Distance), Is.EqualTo((focus, distance)));
            Assert.That(camera.FarthestDistance, Is.GreaterThan(farthest));
        }
    }
}
