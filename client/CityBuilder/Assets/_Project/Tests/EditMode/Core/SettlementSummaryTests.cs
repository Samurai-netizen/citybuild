#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Settlement;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// <see cref="SettlementSummary"/> (M0-53; the couriers since CR-0008-T1): it holds what the server's settlement said, keeps its own copy of the lists, and refuses values no
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
                new[] { new KeyValuePair<ItemId, long>(new ItemId("logs"), 960) },
                new[] { new KeyValuePair<ItemId, long>(new ItemId("logs"), 940) },
                14_045,
                new[] { new TiredCourier(3, 1, 15_095) },
                new[] { 7 },
                new[] { new ResearchId("frame_saw") },
                true);

            Assert.That(summary.ElapsedSeconds, Is.EqualTo(26_000L));
            Assert.That(summary.CourierSeconds, Is.EqualTo(14_045L));
            Assert.That((summary.TiredCouriers[0].StationId, summary.TiredCouriers[0].Slot, summary.TiredCouriers[0].At), Is.EqualTo((3, 1, 15_095L)));
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
            var tired = new List<TiredCourier> { new TiredCourier(3, 1, 200) };

            var summary = new SettlementSummary(
                300, produced, Array.Empty<KeyValuePair<ItemId, long>>(), 100, tired, buildings, Array.Empty<ResearchId>(), false);
            buildings.Add(2);
            produced.Clear();
            tired.Clear();

            Assert.That(summary.CompletedBuildings, Is.EqualTo(new[] { 1 }));
            Assert.That(summary.Produced.Count, Is.EqualTo(1));
            Assert.That(summary.TiredCouriers.Count, Is.EqualTo(1));
        }

        /// <summary>Negative time, negative courier seconds and a negative quantity are no settlement.</summary>
        [Test]
        public void Constructor_ValuesNoSettlementHas_Throw()
        {
            KeyValuePair<ItemId, long>[] none = Array.Empty<KeyValuePair<ItemId, long>>();
            KeyValuePair<ItemId, long>[] negative = { new KeyValuePair<ItemId, long>(new ItemId("logs"), -1) };
            TiredCourier[] noone = Array.Empty<TiredCourier>();

            Assert.Throws<ArgumentException>(() => new SettlementSummary(-1, none, none, 0, noone, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentException>(() => new SettlementSummary(100, none, none, -1, noone, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentException>(() => new SettlementSummary(100, negative, none, 0, noone, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentException>(() => new SettlementSummary(100, none, negative, 0, noone, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
        }

        /// <summary>A missing list is a coding error, not a settlement.</summary>
        [Test]
        public void Constructor_NullList_Throws()
        {
            KeyValuePair<ItemId, long>[] none = Array.Empty<KeyValuePair<ItemId, long>>();

            TiredCourier[] noone = Array.Empty<TiredCourier>();

            Assert.Throws<ArgumentNullException>(() => new SettlementSummary(1, null!, none, 0, noone, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentNullException>(() => new SettlementSummary(1, none, null!, 0, noone, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentNullException>(() => new SettlementSummary(1, none, none, 0, null!, Array.Empty<int>(), Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentNullException>(() => new SettlementSummary(1, none, none, 0, noone, null!, Array.Empty<ResearchId>(), false));
            Assert.Throws<ArgumentNullException>(() => new SettlementSummary(1, none, none, 0, noone, Array.Empty<int>(), null!, false));
        }

        /// <summary>A snapshot made without an answer has no settlement.</summary>
        [Test]
        public void CitySnapshot_WithoutASettlement_HasNone()
        {
            Assert.That(CityTestData.EmptySnapshot(1, 10).Settlement, Is.Null);
        }
    }
}
