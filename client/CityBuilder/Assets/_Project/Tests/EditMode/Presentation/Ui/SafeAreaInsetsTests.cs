#nullable enable

using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Ui
{
    /// <summary>The safe-area maths of the screens (M0-45): pixels of the device's safe area as padding in panel units.</summary>
    public sealed class SafeAreaInsetsTests
    {
        /// <summary>A safe area that is the whole screen needs no padding.</summary>
        [Test]
        public void From_WholeScreen_HasNoInsets()
        {
            SafeAreaInsets insets = SafeAreaInsets.From(new Rect(0, 0, 2000, 1000), 2000, 1000, 1200, 600);

            Assert.That(insets.Left, Is.Zero);
            Assert.That(insets.Top, Is.Zero);
            Assert.That(insets.Right, Is.Zero);
            Assert.That(insets.Bottom, Is.Zero);
        }

        /// <summary>A notch on the left and a home bar at the bottom are scaled from pixels to panel units; the screen's origin is the bottom left.</summary>
        [Test]
        public void From_NotchAndHomeBar_AreScaledToPanelUnits()
        {
            // 2000 x 1000 pixels shown as a 1000 x 500 panel (scale 0.5): 100 px left, 60 px right, 40 px bottom, 20 px top.
            var safe = new Rect(100, 40, 1840, 940);

            SafeAreaInsets insets = SafeAreaInsets.From(safe, 2000, 1000, 1000, 500);

            Assert.That(insets.Left, Is.EqualTo(50f).Within(0.001f));
            Assert.That(insets.Right, Is.EqualTo(30f).Within(0.001f));
            Assert.That(insets.Bottom, Is.EqualTo(20f).Within(0.001f));
            Assert.That(insets.Top, Is.EqualTo(10f).Within(0.001f));
        }

        /// <summary>A zero screen or panel (before the first layout) gives no insets rather than dividing by zero.</summary>
        [TestCase(0, 1000, 1200f, 600f)]
        [TestCase(2000, 0, 1200f, 600f)]
        [TestCase(2000, 1000, 0f, 600f)]
        [TestCase(2000, 1000, 1200f, 0f)]
        public void From_ZeroSizes_HasNoInsets(int screenWidth, int screenHeight, float panelWidth, float panelHeight)
        {
            SafeAreaInsets insets = SafeAreaInsets.From(new Rect(10, 10, 100, 100), screenWidth, screenHeight, panelWidth, panelHeight);

            Assert.That(insets.Left + insets.Top + insets.Right + insets.Bottom, Is.Zero);
        }

        /// <summary>A safe area that reaches past the screen is clamped: an inset is never negative.</summary>
        [Test]
        public void From_SafeAreaBeyondTheScreen_ClampsToZero()
        {
            SafeAreaInsets insets = SafeAreaInsets.From(new Rect(-20, -20, 2100, 1100), 2000, 1000, 2000, 1000);

            Assert.That(insets.Left, Is.Zero);
            Assert.That(insets.Bottom, Is.Zero);
            Assert.That(insets.Right, Is.Zero);
            Assert.That(insets.Top, Is.Zero);
        }
    }
}
