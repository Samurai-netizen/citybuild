#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>The city camera's maths (M0-43): the framing, panning inside the grid, the zoom limits and where the camera stands.</summary>
    public sealed class CityCameraModelTests
    {
        /// <summary>A model over the M0 content's 8 by 8 grid.</summary>
        private CityCameraModel _camera = null!;

        /// <summary>Before each test: a fresh model.</summary>
        [SetUp]
        public void CreateCamera()
        {
            _camera = new CityCameraModel(new CityGrid(8, 8));
        }

        /// <summary>A new camera looks at the middle of the grid from the farthest distance.</summary>
        [Test]
        public void New_LooksAtTheMiddle_FromTheFarthestDistance()
        {
            Assert.That(_camera.Focus, Is.EqualTo(new Vector2(4f, 4f)));
            Assert.That(_camera.Distance, Is.EqualTo(_camera.FarthestDistance));
            Assert.That(_camera.FarthestDistance, Is.GreaterThan(CityCameraModel.NearestDistance));
        }

        /// <summary>The camera's line of sight passes through the focus on the ground, at the set distance.</summary>
        [Test]
        public void Pose_LooksAtTheFocus_AtTheDistance()
        {
            _camera.Pan(new Vector2(-1.5f, 2f));
            _camera.Zoom(0.5f);

            Vector3 ahead = _camera.Position + _camera.Rotation * Vector3.forward * _camera.Distance;

            Assert.That(ahead.x, Is.EqualTo(_camera.Focus.x).Within(0.001f));
            Assert.That(ahead.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(ahead.z, Is.EqualTo(_camera.Focus.y).Within(0.001f));
            Assert.That(_camera.Position.y, Is.GreaterThan(0f), "The camera is above the ground");
        }

        /// <summary>The view is angled: it looks down, not straight down and not level.</summary>
        [Test]
        public void Rotation_LooksDownAtTheSetAngles()
        {
            Vector3 angles = _camera.Rotation.eulerAngles;

            Assert.That(angles.x, Is.EqualTo(CityCameraModel.PitchDegrees).Within(0.001f));
            Assert.That(angles.y, Is.EqualTo(CityCameraModel.YawDegrees).Within(0.001f));
            Assert.That(CityCameraModel.PitchDegrees, Is.InRange(20f, 70f));
        }

        /// <summary>A pan moves the focus by the given ground distance.</summary>
        [Test]
        public void Pan_MovesTheFocus()
        {
            _camera.Pan(new Vector2(1f, -2f));

            Assert.That(_camera.Focus, Is.EqualTo(new Vector2(5f, 2f)));
        }

        /// <summary>The focus never leaves the grid's outline, however far the pan goes.</summary>
        [TestCase(100f, 0f, 8f, 4f)]
        [TestCase(-100f, 0f, 0f, 4f)]
        [TestCase(0f, 100f, 4f, 8f)]
        [TestCase(0f, -100f, 4f, 0f)]
        [TestCase(-100f, -100f, 0f, 0f)]
        public void Pan_StopsAtTheOutline(float dx, float dz, float expectedX, float expectedZ)
        {
            _camera.Pan(new Vector2(dx, dz));

            Assert.That(_camera.Focus, Is.EqualTo(new Vector2(expectedX, expectedZ)));
        }

        /// <summary>A pan that is not a number changes nothing.</summary>
        [Test]
        public void Pan_WithNaN_ChangesNothing()
        {
            _camera.Pan(new Vector2(float.NaN, 1f));

            Assert.That(_camera.Focus, Is.EqualTo(new Vector2(4f, 4f)));
        }

        /// <summary>Zooming in and out stays between the near and the far limit.</summary>
        [Test]
        public void Zoom_StaysBetweenTheLimits()
        {
            _camera.Zoom(0.0001f);
            Assert.That(_camera.Distance, Is.EqualTo(CityCameraModel.NearestDistance));

            _camera.Zoom(10000f);
            Assert.That(_camera.Distance, Is.EqualTo(_camera.FarthestDistance));
        }

        /// <summary>A factor below 1 comes closer and one above 1 goes away.</summary>
        [Test]
        public void Zoom_ChangesTheDistanceByTheFactor()
        {
            float start = _camera.Distance;

            _camera.Zoom(0.8f);
            Assert.That(_camera.Distance, Is.EqualTo(start * 0.8f).Within(0.0001f));

            _camera.Zoom(1.25f);
            Assert.That(_camera.Distance, Is.EqualTo(start).Within(0.0001f));
        }

        /// <summary>A factor that is zero, negative or not a number is ignored.</summary>
        [TestCase(0f)]
        [TestCase(-2f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Zoom_WithABadFactor_ChangesNothing(float factor)
        {
            float start = _camera.Distance;

            _camera.Zoom(factor);

            Assert.That(_camera.Distance, Is.EqualTo(start));
        }

        /// <summary>A portrait screen needs a larger distance than a landscape one to see the whole grid.</summary>
        [Test]
        public void Frame_OnANarrowScreen_IsFartherThanOnAWideOne()
        {
            _camera.Frame(16f / 9f);
            float wide = _camera.FarthestDistance;

            _camera.Frame(9f / 16f);

            Assert.That(_camera.FarthestDistance, Is.GreaterThan(wide));
        }

        /// <summary>Framing again goes back to the middle at the farthest distance, and an unusable aspect counts as square.</summary>
        [Test]
        public void Frame_ResetsTheView_AndToleratesABadAspect()
        {
            _camera.Pan(new Vector2(3f, 3f));
            _camera.Zoom(0.1f);
            _camera.Frame(1f);
            float square = _camera.FarthestDistance;

            _camera.Pan(new Vector2(3f, 3f));
            _camera.Frame(float.NaN);

            Assert.That(_camera.Focus, Is.EqualTo(new Vector2(4f, 4f)));
            Assert.That(_camera.Distance, Is.EqualTo(_camera.FarthestDistance));
            Assert.That(_camera.FarthestDistance, Is.EqualTo(square));
        }

        /// <summary>The corners of the grid are inside the view when the camera is framed on a square screen: the farthest distance fits the grid's bounding circle.</summary>
        [Test]
        public void Frame_FitsTheGridsBoundingCircle()
        {
            float radius = new CityGrid(8, 8).WorldSize.magnitude * 0.5f;
            float seen = _camera.FarthestDistance * Mathf.Tan(CityCameraModel.FieldOfViewDegrees * 0.5f * Mathf.Deg2Rad);

            Assert.That(seen, Is.GreaterThan(radius));
        }
    }
}
