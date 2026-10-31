#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>Tap or drag for one pointer, and the pinch's zoom factor (M0-43).</summary>
    public sealed class PointerGestureTests
    {
        /// <summary>A reader with a 10 pixel threshold; a new one for each test, because NUnit uses one fixture object for all of them.</summary>
        private PointerGesture _gesture = null!;

        /// <summary>Before each test: creates the reader.</summary>
        [SetUp]
        public void CreateGesture()
        {
            _gesture = new PointerGesture(10f);
        }

        /// <summary>A press and release without movement is a tap.</summary>
        [Test]
        public void PressAndRelease_WithoutMoving_IsATap()
        {
            _gesture.Press(new Vector2(100f, 100f));

            Assert.That(_gesture.IsDown, Is.True);
            Assert.That(_gesture.Release(), Is.True);
            Assert.That(_gesture.IsDown, Is.False);
        }

        /// <summary>A small wobble within the threshold is still a tap and reports no movement.</summary>
        [Test]
        public void SmallMovement_IsStillATap_AndReportsNothing()
        {
            _gesture.Press(new Vector2(100f, 100f));

            Vector2 delta = _gesture.Move(new Vector2(105f, 104f));

            Assert.That(delta, Is.EqualTo(Vector2.zero));
            Assert.That(_gesture.IsDragging, Is.False);
            Assert.That(_gesture.Release(), Is.True);
        }

        /// <summary>The move that crosses the threshold reports all movement since the press; later moves report their own.</summary>
        [Test]
        public void Dragging_ReportsTheMovement_WithoutLosingTheFirstPart()
        {
            _gesture.Press(new Vector2(100f, 100f));

            Vector2 first = _gesture.Move(new Vector2(120f, 100f));
            Vector2 second = _gesture.Move(new Vector2(125f, 90f));

            Assert.That(_gesture.IsDragging, Is.True);
            Assert.That(first, Is.EqualTo(new Vector2(20f, 0f)));
            Assert.That(second, Is.EqualTo(new Vector2(5f, -10f)));
        }

        /// <summary>A drag that returns to where it began is still a drag, so releasing it is not a tap.</summary>
        [Test]
        public void ADragThatComesBack_IsNotATap()
        {
            _gesture.Press(new Vector2(100f, 100f));
            _gesture.Move(new Vector2(150f, 100f));
            _gesture.Move(new Vector2(100f, 100f));

            Assert.That(_gesture.Release(), Is.False);
        }

        /// <summary>With no press, a move reports nothing and a release is no tap.</summary>
        [Test]
        public void WithoutAPress_NothingHappens()
        {
            Assert.That(_gesture.Move(new Vector2(50f, 50f)), Is.EqualTo(Vector2.zero));
            Assert.That(_gesture.Release(), Is.False);
        }

        /// <summary>A cancelled press is forgotten and does not end as a tap.</summary>
        [Test]
        public void Cancel_ForgetsThePress()
        {
            _gesture.Press(new Vector2(100f, 100f));

            _gesture.Cancel();

            Assert.That(_gesture.IsDown, Is.False);
            Assert.That(_gesture.Release(), Is.False);
        }

        /// <summary>A new press after a drag starts clean.</summary>
        [Test]
        public void ANewPress_StartsClean()
        {
            _gesture.Press(new Vector2(0f, 0f));
            _gesture.Move(new Vector2(50f, 0f));
            _gesture.Release();

            _gesture.Press(new Vector2(200f, 200f));

            Assert.That(_gesture.IsDragging, Is.False);
            Assert.That(_gesture.Release(), Is.True);
        }

        /// <summary>The first call of a pinch is a factor of 1, fingers moving apart give a factor below 1 and together above 1.</summary>
        [Test]
        public void Pinch_GivesTheZoomFactor()
        {
            var pinch = new PinchGesture();

            Assert.That(pinch.IsActive, Is.False);
            Assert.That(pinch.Update(new Vector2(0f, 0f), new Vector2(100f, 0f)), Is.EqualTo(1f));
            Assert.That(pinch.IsActive, Is.True);
            Assert.That(pinch.Update(new Vector2(0f, 0f), new Vector2(200f, 0f)), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(pinch.Update(new Vector2(0f, 0f), new Vector2(100f, 0f)), Is.EqualTo(2f).Within(0.0001f));
        }

        /// <summary>After a pinch ends the next one starts at 1 again; fingers on one spot give 1 and end the pinch.</summary>
        [Test]
        public void Pinch_EndsAndRestarts_AndToleratesFingersOnOneSpot()
        {
            var pinch = new PinchGesture();
            pinch.Update(new Vector2(0f, 0f), new Vector2(100f, 0f));

            pinch.End();
            Assert.That(pinch.IsActive, Is.False);
            Assert.That(pinch.Update(new Vector2(0f, 0f), new Vector2(300f, 0f)), Is.EqualTo(1f));

            Assert.That(pinch.Update(new Vector2(5f, 5f), new Vector2(5f, 5f)), Is.EqualTo(1f));
            Assert.That(pinch.IsActive, Is.False);
        }
    }
}
