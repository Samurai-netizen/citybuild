#nullable enable

using System;
using CityBuilder.Client.Presentation.City;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// <see cref="WalkPath"/> (CR-0008-T2 part D): a walker's place along a walk from tile centre to tile centre, spread over the steps by their
    /// weights, with every turn a right angle.
    /// </summary>
    public sealed class WalkPathTests
    {
        /// <summary>An L-shaped walk with two steps of the same weight: the places at the start, the corner, the middle of each step and the end.</summary>
        [Test]
        public void Sample_AlongAnLShapedWalk_MovesFromCentreToCentreWithARightAngleTurn()
        {
            var path = new WalkPath(new[] { (0, 0), (1, 0), (1, 1) }, new long[] { 10, 10 });

            path.Sample(0f, out float x, out float z, out int dx, out int dz);
            Assert.That((x, z), Is.EqualTo((0.5f, 0.5f)));
            Assert.That((dx, dz), Is.EqualTo((1, 0)));

            path.Sample(0.25f, out x, out z, out dx, out dz);
            Assert.That(x, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(z, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That((dx, dz), Is.EqualTo((1, 0)));

            path.Sample(0.75f, out x, out z, out dx, out dz);
            Assert.That(x, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(z, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That((dx, dz), Is.EqualTo((0, 1)));

            path.Sample(1f, out x, out z, out _, out _);
            Assert.That((x, z), Is.EqualTo((1.5f, 1.5f)));
        }

        /// <summary>A heavy step (a crossing) takes the bigger share of the walk's time: at 75 % the walker has just finished the 3:1 first step.</summary>
        [Test]
        public void Sample_SpreadsTheTimeByTheStepWeights()
        {
            var path = new WalkPath(new[] { (0, 0), (1, 0), (1, 1) }, new long[] { 30, 10 });

            path.Sample(0.75f, out float x, out float z, out _, out _);

            Assert.That(x, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(z, Is.EqualTo(0.5f).Within(1e-5f));
        }

        /// <summary>A fraction outside 0 to 1 gives the start or the end.</summary>
        [Test]
        public void Sample_ClampsTheFraction()
        {
            var path = new WalkPath(new[] { (2, 2), (3, 2) }, new long[] { 5 });

            path.Sample(-4f, out float x, out _, out _, out _);
            Assert.That(x, Is.EqualTo(2.5f));

            path.Sample(9f, out x, out _, out _, out _);
            Assert.That(x, Is.EqualTo(3.5f));
        }

        /// <summary>A path of one tile stands there and has no direction.</summary>
        [Test]
        public void Standing_StaysOnItsTileWithoutADirection()
        {
            WalkPath path = WalkPath.Standing(4, 5);

            path.Sample(0.5f, out float x, out float z, out int dx, out int dz);

            Assert.That((x, z, dx, dz), Is.EqualTo((4.5f, 5.5f, 0, 0)));
            Assert.That(path.StepCount, Is.EqualTo(0));
        }

        /// <summary>The weights must be one per step.</summary>
        [Test]
        public void Constructor_WithTheWrongNumberOfWeights_Throws()
        {
            Assert.Throws<ArgumentException>(() => new WalkPath(new[] { (0, 0), (1, 0) }, new long[] { 1, 1 }));
            Assert.Throws<ArgumentException>(() => new WalkPath(Array.Empty<(int, int)>(), Array.Empty<long>()));
        }
    }
}
