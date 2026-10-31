#nullable enable

using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Ui
{
    /// <summary>The bottom-left to top-left conversion that keeps a press on the UI from reaching the ground (M0-45).</summary>
    public sealed class ScreenPointTests
    {
        /// <summary>A point near the bottom of the screen is near the bottom of the panel's top-left space, and x is untouched.</summary>
        [TestCase(100f, 20f, 1000f, 100f, 980f)]
        [TestCase(0f, 0f, 480f, 0f, 480f)]
        [TestCase(640f, 480f, 480f, 640f, 0f)]
        public void ToTopLeft_FlipsOnlyY(float x, float y, float screenHeight, float expectedX, float expectedY)
        {
            Vector2 point = ScreenPoint.ToTopLeft(new Vector2(x, y), screenHeight);

            Assert.That(point.x, Is.EqualTo(expectedX));
            Assert.That(point.y, Is.EqualTo(expectedY));
        }

        /// <summary>Flipping twice gives the point back.</summary>
        [Test]
        public void ToTopLeft_Twice_IsTheIdentity()
        {
            var start = new Vector2(37f, 412f);

            Assert.That(ScreenPoint.ToTopLeft(ScreenPoint.ToTopLeft(start, 800f), 800f), Is.EqualTo(start));
        }
    }
}
