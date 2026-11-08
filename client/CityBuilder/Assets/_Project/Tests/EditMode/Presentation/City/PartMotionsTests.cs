#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// The table of moving parts of visual v2 (<see cref="PartMotions"/>): it has a row for every part the art uses, numbered copies find their
    /// base row, and other names are not parts.
    /// </summary>
    public sealed class PartMotionsTests
    {
        /// <summary>Every moving part of the art has a row of the right kind.</summary>
        [TestCase("Wheel", MotionKind.Spin)]
        [TestCase("Sign", MotionKind.Spin)]
        [TestCase("Vane", MotionKind.Spin)]
        [TestCase("Axe", MotionKind.Chop)]
        [TestCase("Hammer", MotionKind.Chop)]
        [TestCase("Saw", MotionKind.PingPong)]
        [TestCase("Hoist", MotionKind.PingPong)]
        [TestCase("Flag", MotionKind.Wave)]
        [TestCase("Leg_L", MotionKind.Wave)]
        [TestCase("Arm_R", MotionKind.Wave)]
        public void TheArtsParts_HaveRows(string name, MotionKind kind)
        {
            Assert.That(PartMotions.TryGet(name, out PartMotion motion), Is.True, name);
            Assert.That(motion.Kind, Is.EqualTo(kind), name);
        }

        /// <summary>A numbered copy of a part (a second flag) moves like the part.</summary>
        [Test]
        public void ANumberedCopy_FindsItsBaseRow()
        {
            Assert.That(PartMotions.TryGet("Flag_2", out PartMotion motion), Is.True);
            Assert.That(motion.Kind, Is.EqualTo(MotionKind.Wave));
            Assert.That(PartMotions.Has("Flag_2"), Is.False);
        }

        /// <summary>The legs move in opposite steps, each arm against its leg.</summary>
        [Test]
        public void TheLimbs_MoveInStep()
        {
            PartMotions.TryGet("Leg_L", out PartMotion legL);
            PartMotions.TryGet("Leg_R", out PartMotion legR);
            PartMotions.TryGet("Arm_L", out PartMotion armL);
            Assert.That(legR.Phase - legL.Phase, Is.EqualTo(0.5f));
            Assert.That(armL.Phase - legL.Phase, Is.EqualTo(0.5f));
        }

        /// <summary>Anchors, poses, store steps and other names are not moving parts.</summary>
        [TestCase("Anchor_Status")]
        [TestCase("Wheel_Pose")]
        [TestCase("Fill_logs_1")]
        [TestCase("Flag_")]
        [TestCase("_2")]
        [TestCase("Flag_x")]
        [TestCase("")]
        public void OtherNames_AreNotParts(string name)
        {
            Assert.That(PartMotions.TryGet(name, out _), Is.False, name);
        }
    }
}
