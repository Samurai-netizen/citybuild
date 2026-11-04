#nullable enable

using System;
using CityBuilder.Client.Presentation.City;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>The steps a store shows (<see cref="StorageFill"/>): one per equal part of the limit, none when empty, all when full.</summary>
    public sealed class StorageFillTests
    {
        /// <summary>Three steps over a limit of 40 (a Woodcutter's Hut at level 1, CR-0008).</summary>
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(13, 1)]
        [TestCase(14, 2)]
        [TestCase(26, 2)]
        [TestCase(27, 3)]
        [TestCase(39, 3)]
        [TestCase(40, 3)]
        [TestCase(55, 3)]
        public void ThreeSteps_SplitTheLimitInThirds(long amount, int expected)
        {
            Assert.That(StorageFill.StepFor(amount, 40, 3), Is.EqualTo(expected));
        }

        /// <summary>Two steps over a limit of 40 (the Sawmill's logs): the second from half.</summary>
        [TestCase(1, 1)]
        [TestCase(19, 1)]
        [TestCase(20, 2)]
        [TestCase(40, 2)]
        public void TwoSteps_SplitTheLimitInHalves(long amount, int expected)
        {
            Assert.That(StorageFill.StepFor(amount, 40, 2), Is.EqualTo(expected));
        }

        /// <summary>One step shows as soon as there is anything.</summary>
        [Test]
        public void OneStep_ShowsAnything()
        {
            Assert.That(StorageFill.StepFor(1, 500, 1), Is.EqualTo(1));
            Assert.That(StorageFill.StepFor(0, 500, 1), Is.EqualTo(0));
        }

        /// <summary>An item without a limit, or a negative amount, shows nothing; a huge limit does not overflow.</summary>
        [Test]
        public void NoLimit_OrNothing_ShowsNothing_AndHugeNumbersAreSafe()
        {
            Assert.That(StorageFill.StepFor(10, 0, 3), Is.EqualTo(0));
            Assert.That(StorageFill.StepFor(-5, 40, 3), Is.EqualTo(0));
            Assert.That(StorageFill.StepFor(long.MaxValue - 1, long.MaxValue, 3), Is.EqualTo(3));
        }

        /// <summary>A fill model has one to three steps.</summary>
        [TestCase(0)]
        [TestCase(4)]
        public void AStepCountOutsideOneToThree_IsRefused(int steps)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StorageFill.StepFor(1, 10, steps));
        }
    }
}
