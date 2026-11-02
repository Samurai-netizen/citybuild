#nullable enable

using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Ui
{
    /// <summary>
    /// <see cref="ScaleToFit"/> (M0-59, owner directive OD-17): content that does not fit is scaled down as a whole, and its layout space shrinks
    /// with it; content that fits keeps its size.
    /// </summary>
    public sealed class ScaleToFitTests
    {
        /// <summary>Content that fits, exactly or with room, is not scaled.</summary>
        [TestCase(200f, 204f)]
        [TestCase(204f, 204f)]
        public void Factor_ContentThatFits_IsOne(float needed, float available)
        {
            Assert.That(ScaleToFit.Factor(needed, available), Is.EqualTo(1f));
        }

        /// <summary>Content that is too big gets the share of it that fits: 220 units of cards in 204 units of panel are scaled to 204 / 220.</summary>
        [Test]
        public void Factor_ContentTooBig_IsTheShareThatFits()
        {
            Assert.That(ScaleToFit.Factor(220f, 204f), Is.EqualTo(204f / 220f).Within(0.0001f));
        }

        /// <summary>An unknown size, as before the first layout or while a panel is hidden, never shrinks anything.</summary>
        [TestCase(0f, 204f)]
        [TestCase(220f, 0f)]
        [TestCase(-1f, 204f)]
        [TestCase(float.NaN, 204f)]
        [TestCase(220f, float.NaN)]
        public void Factor_AnUnknownSize_IsOne(float needed, float available)
        {
            Assert.That(ScaleToFit.Factor(needed, available), Is.EqualTo(1f));
        }

        /// <summary>Scaling to a lower height scales from the top left and gives back the space the content no longer takes, on the right and below.</summary>
        [Test]
        public void Height_ContentTooHigh_ScalesFromTheTopLeft_AndShrinksItsSpace()
        {
            var content = new VisualElement();

            float factor = ScaleToFit.Height(content, new Vector2(1000f, 220f), 176f);

            Assert.That(factor, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(content.style.scale.value.value.x, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(content.style.scale.value.value.y, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(content.style.transformOrigin.value.x.value, Is.EqualTo(0f));
            Assert.That(content.style.transformOrigin.value.y.value, Is.EqualTo(0f));
            Assert.That(content.style.marginRight.value.value, Is.EqualTo(-200f).Within(0.01f));
            Assert.That(content.style.marginBottom.value.value, Is.EqualTo(-44f).Within(0.01f));
        }

        /// <summary>Content that fits again, after a turn of the phone, goes back to its full size and its full space.</summary>
        [Test]
        public void Height_ContentThatFitsAgain_IsBackToFullSize()
        {
            var content = new VisualElement();
            ScaleToFit.Height(content, new Vector2(1000f, 220f), 176f);

            float factor = ScaleToFit.Height(content, new Vector2(1000f, 220f), 500f);

            Assert.That(factor, Is.EqualTo(1f));
            Assert.That(content.style.scale.value.value.x, Is.EqualTo(1f));
            Assert.That(content.style.marginRight.value.value, Is.EqualTo(0f));
            Assert.That(content.style.marginBottom.value.value, Is.EqualTo(0f));
        }
    }
}
