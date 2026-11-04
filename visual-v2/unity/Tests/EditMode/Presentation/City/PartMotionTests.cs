#nullable enable

using System;
using CityBuilder.Client.Presentation.City;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// The motion formula of visual v2 (<see cref="PartMotion"/>): each kind's weight at the points the art's previews rely on, the period
    /// and the phase, and the refusal of a cycle without time. The art pipeline's <c>motion_weight</c> gives the same values.
    /// </summary>
    public sealed class PartMotionTests
    {
        /// <summary>The tolerance for float weights.</summary>
        private const float Tolerance = 1e-4f;

        /// <summary>A chop rests at 0, reaches the pose at 0.62 of its cycle, is back at rest by 0.74 and stays there.</summary>
        [TestCase(0f, 0f)]
        [TestCase(0.31f, 0.5f)]
        [TestCase(0.62f, 1f)]
        [TestCase(0.74f, 0f)]
        [TestCase(0.9f, 0f)]
        [TestCase(1.62f, 1f)]
        public void Chop_RisesSlowly_DropsFast_AndRests(float cycles, float expected)
        {
            Assert.That(PartMotion.WeightAt(MotionKind.Chop, cycles), Is.EqualTo(expected).Within(Tolerance));
        }

        /// <summary>The blow back to rest is quicker than the rise: halfway through the blow the axe is still most of the way up.</summary>
        [Test]
        public void Chop_Blow_Accelerates()
        {
            Assert.That(PartMotion.WeightAt(MotionKind.Chop, 0.68f), Is.EqualTo(0.75f).Within(Tolerance));
        }

        /// <summary>A ping-pong is at rest at the start of its cycle and at the pose in the middle.</summary>
        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.5f)]
        [TestCase(0.5f, 1f)]
        [TestCase(0.75f, 0.5f)]
        [TestCase(2f, 0f)]
        public void PingPong_GoesToThePose_AndBack(float cycles, float expected)
        {
            Assert.That(PartMotion.WeightAt(MotionKind.PingPong, cycles), Is.EqualTo(expected).Within(Tolerance));
        }

        /// <summary>A wave reaches the pose at a quarter and its mirror at three quarters.</summary>
        [TestCase(0f, 0f)]
        [TestCase(0.25f, 1f)]
        [TestCase(0.5f, 0f)]
        [TestCase(0.75f, -1f)]
        public void Wave_SwingsBothWays(float cycles, float expected)
        {
            Assert.That(PartMotion.WeightAt(MotionKind.Wave, cycles), Is.EqualTo(expected).Within(Tolerance));
        }

        /// <summary>A spin counts turns.</summary>
        [Test]
        public void Spin_CountsTurns()
        {
            Assert.That(PartMotion.WeightAt(MotionKind.Spin, 2.25f), Is.EqualTo(2.25f).Within(Tolerance));
        }

        /// <summary>The weight after some seconds is the weight at seconds over the period plus the phase.</summary>
        [Test]
        public void Weight_UsesThePeriodAndThePhase()
        {
            var motion = new PartMotion(MotionKind.PingPong, 2f, 0.25f);
            Assert.That(motion.Weight(0f), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(motion.Weight(0.5f), Is.EqualTo(1f).Within(Tolerance));
        }

        /// <summary>A cycle must take some time.</summary>
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void APeriodWithoutTime_IsRefused(float period)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PartMotion(MotionKind.Spin, period, 0f));
        }
    }
}
