#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Core;
using CityBuilder.Simulation;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// <see cref="SettlementSummary"/> (M0-53): it holds what the server's settlement said, keeps its own copy of the lists, and refuses values no
    /// settlement has.
    /// </summary>
    public sealed class SettlementSummaryTests
    {
        /// <summary>Every value comes back as given.</summary>
        [Test]
        public void Constructor_KeepsEveryValue()
        {
            var summary = new SettlementSummary(
                26_000,
                28_800 - 3_000,
                new[] { new KeyValuePair<ItemId, long>(new ItemId("logs"), 960) },
                new[] { new KeyValuePair<ItemId, long>(new ItemId("logs"), 940) },
                new[] { 7 },
                new[] { new ResearchId("frame_saw") },
                true);

            Assert.That(summary.ElapsedSeconds, Is.EqualTo(26_000L));
            Assert.That(summary.ProductionSeconds, Is.EqualTo(25_800L));
            Assert.That(summary.Produced[0].Key.Value, Is.EqualTo("logs"));
            Assert.That(summary.Produced[0].Value, Is.EqualTo(960L));
            Assert.That(summary.Consumed[0].Value, Is.EqualTo(940L));
            Assert.That(summary.CompletedBuildings, Is.EqualTo(new[] { 7 }));
            Assert.That(summary.CompletedResearch[0].Value, Is.EqualTo("frame_saw"));
            Assert.That(summary.EraChanged, Is.True);
        }

        /// <summary>A list the caller changes afterwards does not change the summary.</summary>
        [Test]
        public void Constructor_CopiesTheLists()
        {
            var buildings = new List<int> { 1 };
            var produced = new List<KeyValuePair<ItemId, long>> { new KeyValuePair<ItemId, long>(new ItemId("logs"), 5) };

            var summary = new SettlementSummary(
                300, 300, produced, Array.Empty<KeyValuePair<ItemId, long>>(), buildings, Array.Empty<ResearchId>(), false);
            buildings.Add(2);
            produced.Clear();

            Assert.That(summary.CompletedBuildings, Is.EqualTo(new[] { 1 }));
            Assert.That(summary.Produced.Count, Is.EqualTo(1));
        }

        /// <summary>Negative time, production time above the elapsed time and a negative quantity are no settlement.</summary>
        [Test]
        public void Constructor_ValuesNoSettlementHas_Throw()
        {
            KeyValuePair<ItemId, long>[] none = Array.Empty<KeyValuePair<ItemId, long>>();
            KeyValuePair<ItemId, long>[] negative = { new KeyValuePair<ItemId, long>(new ItemId("logs"), -1) };

            Assert.Throws<ArgumentException>(() => new SettlementSummary(-1, 0, none, none, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentException>(() => new SettlementSummary(100, 101, none, none, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentException>(() => new SettlementSummary(100, 100, negative, none, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentException>(() => new SettlementSummary(100, 100, none, negative, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
        }

        /// <summary>A missing list is a coding error, not a settlement.</summary>
        [Test]
        public void Constructor_NullList_Throws()
        {
            KeyValuePair<ItemId, long>[] none = Array.Empty<KeyValuePair<ItemId, long>>();

            Assert.Throws<ArgumentNullException>(() => new SettlementSummary(1, 1, null!, none, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentNullException>(() => new SettlementSummary(1, 1, none, null!, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentNullException>(() => new SettlementSummary(1, 1, none, none, null!, Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentNullException>(() => new SettlementSummary(1, 1, none, none, Array.Empty<int>(), null!, false));
        }

        /// <summary>A snapshot made without an answer has no settlement.</summary>
        [Test]
        public void CitySnapshot_WithoutASettlement_HasNone()
        {
            Assert.That(CityTestData.EmptySnapshot(1, 10).Settlement, Is.Null);
        }
    }
}
