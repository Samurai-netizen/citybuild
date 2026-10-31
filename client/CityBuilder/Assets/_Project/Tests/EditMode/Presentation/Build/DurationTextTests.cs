#nullable enable

using CityBuilder.Client.Presentation.Build;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Build
{
    /// <summary>How a card words a span of seconds (M0-45): only the units that are not zero, from hours down to seconds.</summary>
    public sealed class DurationTextTests
    {
        /// <summary>The spans the content has and the edges around them.</summary>
        [TestCase(0, "0s")]
        [TestCase(1, "1s")]
        [TestCase(45, "45s")]
        [TestCase(60, "1m")]
        [TestCase(150, "2m 30s")]
        [TestCase(180, "3m")]
        [TestCase(3599, "59m 59s")]
        [TestCase(3600, "1h")]
        [TestCase(3665, "1h 1m 5s")]
        [TestCase(7200, "2h")]
        [TestCase(90000, "25h")]
        public void Format_GivesTheNonZeroUnits(long seconds, string expected)
        {
            Assert.That(DurationText.Format(seconds), Is.EqualTo(expected));
        }

        /// <summary>A negative span cannot come from valid content; it shows as zero rather than a minus sign.</summary>
        [Test]
        public void Format_OfANegativeSpan_IsZero()
        {
            Assert.That(DurationText.Format(-5), Is.EqualTo("0s"));
        }
    }
}
