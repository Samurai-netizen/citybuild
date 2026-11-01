#nullable enable

using CityBuilder.Client.Presentation.Connection;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Connection
{
    /// <summary>How long ago the last sync was, in the coarse words of the connection banner (M0-55).</summary>
    public sealed class SyncAgeTextTests
    {
        /// <summary>Each span has the words of its unit, and the unit changes at the minute and the hour.</summary>
        [TestCase(0L, "just now")]
        [TestCase(4L, "just now")]
        [TestCase(5L, "5s ago")]
        [TestCase(59L, "59s ago")]
        [TestCase(60L, "1m ago")]
        [TestCase(200L, "3m ago")]
        [TestCase(3599L, "59m ago")]
        [TestCase(3600L, "1h ago")]
        [TestCase(3900L, "1h 5m ago")]
        [TestCase(36000L, "10h ago")]
        public void Format_GivesTheCoarseWordsOfTheUnit(long seconds, string expected)
        {
            Assert.That(SyncAgeText.Format(seconds), Is.EqualTo(expected));
        }

        /// <summary>A negative span (a clock that went back) reads as just now, never as a negative number.</summary>
        [Test]
        public void Format_NegativeSpan_IsJustNow()
        {
            Assert.That(SyncAgeText.Format(-30), Is.EqualTo("just now"));
        }
    }
}
