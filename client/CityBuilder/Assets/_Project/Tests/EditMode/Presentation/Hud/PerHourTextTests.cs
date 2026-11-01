#nullable enable

using CityBuilder.Client.Presentation.Hud;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Hud
{
    /// <summary>The words of a net rate on an item chip (M0-49): sign, whole items, one tenth, and the trend.</summary>
    public sealed class PerHourTextTests
    {
        /// <summary>The three forms the task names: a gain, a loss with a real minus sign, and zero.</summary>
        [Test]
        public void Format_GainLossAndZero()
        {
            Assert.That(PerHourText.Format(60000), Is.EqualTo("+60/h"));
            Assert.That(PerHourText.Format(-30000), Is.EqualTo("−30/h"));
            Assert.That(PerHourText.Format(0), Is.EqualTo("0/h"));
        }

        /// <summary>A rate that is not whole shows one tenth, and a loss keeps its sign.</summary>
        [Test]
        public void Format_ShowsOneTenth_WhenNotWhole()
        {
            Assert.That(PerHourText.Format(12500), Is.EqualTo("+12.5/h"));
            Assert.That(PerHourText.Format(-500), Is.EqualTo("−0.5/h"));
            Assert.That(PerHourText.Format(100), Is.EqualTo("+0.1/h"));
        }

        /// <summary>The tenth is rounded toward zero, like every display estimate.</summary>
        [Test]
        public void Format_RoundsTowardZero()
        {
            Assert.That(PerHourText.Format(12599), Is.EqualTo("+12.5/h"));
            Assert.That(PerHourText.Format(-12599), Is.EqualTo("−12.5/h"));
        }

        /// <summary>A rate below a tenth reads zero in either direction, with no sign.</summary>
        [Test]
        public void Format_BelowATenth_ReadsZero()
        {
            Assert.That(PerHourText.Format(99), Is.EqualTo("0/h"));
            Assert.That(PerHourText.Format(-99), Is.EqualTo("0/h"));
        }

        /// <summary>The trend follows what the text shows, so a rate shown as 0/h is flat.</summary>
        [Test]
        public void TrendOf_FollowsTheShownRate()
        {
            Assert.That(PerHourText.TrendOf(60000), Is.EqualTo(RateTrend.Rising));
            Assert.That(PerHourText.TrendOf(-100), Is.EqualTo(RateTrend.Falling));
            Assert.That(PerHourText.TrendOf(0), Is.EqualTo(RateTrend.Flat));
            Assert.That(PerHourText.TrendOf(-99), Is.EqualTo(RateTrend.Flat));
        }
    }
}
