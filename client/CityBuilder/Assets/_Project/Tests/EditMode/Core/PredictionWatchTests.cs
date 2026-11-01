#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// <see cref="PredictionWatch"/> (M0-54): silent when the server's next snapshot is what the engine predicted, and when a command explains the
    /// difference; a report with both cities when it is not; a report when the display goes back.
    /// </summary>
    public sealed class PredictionWatchTests
    {
        /// <summary>The real content.</summary>
        private GameContent _content = null!;

        /// <summary>The fake real-time clock, in seconds.</summary>
        private double _now;

        /// <summary>The store under the watch.</summary>
        private ClientCityStore _store = null!;

        /// <summary>The display city under the watch.</summary>
        private DisplayCity _display = null!;

        /// <summary>What the watch says a command is doing.</summary>
        private bool _commandsPending;

        /// <summary>What the watch reported.</summary>
        private List<string> _reports = null!;

        /// <summary>Before each test: a display city at scale 1 on the real "producing" answer, with a watch.</summary>
        [SetUp]
        public void CreateWatch()
        {
            _content = CityTestData.Content();
            _now = 100;
            _commandsPending = false;
            _reports = new List<string>();
            _store = new ClientCityStore(() => _now);
            _store.Apply(CityTestData.Snapshot("snapshot-city-producing.json"));
            _display = new DisplayCity(_store, new DisplayClock(_store, 1), _content);
            _ = new PredictionWatch(_store, _display, _content, () => _commandsPending, _reports.Add);
        }

        /// <summary>The snapshot the server would send <paramref name="seconds"/> after the one held, with a higher version.</summary>
        private CitySnapshot Later(long seconds)
        {
            CitySnapshot held = _store.Snapshot!;
            CityState city = held.CopyCity();
            Simulator.Advance(city, _content, seconds);
            return new CitySnapshot(held.StateVersion + 1, held.ContentVersion, city);
        }

        /// <summary>A snapshot that is what the engine predicts, with the display behind it, says nothing.</summary>
        [Test]
        public void ASnapshotAsPredicted_ReportsNothing()
        {
            _now += 100;
            _display.Refresh();

            _store.Apply(Later(120));

            Assert.That(_reports, Is.Empty);
        }

        /// <summary>A city that differs from the prediction is reported with the cities as JSON, the text a regression test starts from.</summary>
        [Test]
        public void ASnapshotThatDiffers_ReportsBothCities()
        {
            CitySnapshot later = Later(0);
            var changed = new List<KeyValuePair<ItemId, long>>(later.CopyCity().Inventory);
            for (int index = 0; index < changed.Count; index++)
            {
                changed[index] = new KeyValuePair<ItemId, long>(changed[index].Key, changed[index].Value + 1);
            }

            CityState city = later.CopyCity();
            var other = new CityState(city.Clock, city.Era, city.NextBuildingId, changed, city.Buildings, city.CompletedResearch, city.ActiveResearch);
            string previous = PredictionWatch.ToJson(_store.Snapshot!.CopyCity());

            _store.Apply(new CitySnapshot(later.StateVersion, later.ContentVersion, other));

            Assert.That(_reports, Has.Count.EqualTo(1));
            Assert.That(_reports[0], Does.Contain("mismatch"));
            Assert.That(_reports[0], Does.Contain(previous));
            Assert.That(_reports[0], Does.Contain(PredictionWatch.ToJson(other)));
        }

        /// <summary>A command's answer changes the city on purpose: no mismatch while a command is on its way.</summary>
        [Test]
        public void ASnapshotThatDiffers_WhileACommandIsPending_ReportsNothing()
        {
            CitySnapshot later = Later(0);
            CityState city = later.CopyCity();
            var empty = new CityState(city.Clock, city.Era, city.NextBuildingId, new List<KeyValuePair<ItemId, long>>(), city.Buildings, city.CompletedResearch, city.ActiveResearch);
            _commandsPending = true;

            _store.Apply(new CitySnapshot(later.StateVersion, later.ContentVersion, empty));

            Assert.That(_reports, Is.Empty);
        }

        /// <summary>A snapshot older than the display's clock, the answer that took longer than the one before, is reported with the counters that fell.</summary>
        [Test]
        public void ASnapshotBehindTheDisplay_ReportsTheClockGoingBack()
        {
            _now += 100;
            _display.Refresh();
            CityState shown = _display.Current!;
            Assert.That(shown.Clock, Is.EqualTo(_store.Snapshot!.Clock + 100));

            _store.Apply(Later(40));

            Assert.That(_reports, Has.Count.EqualTo(1));
            Assert.That(_reports[0], Does.Contain("the clock went back 60 s"));
        }

        /// <summary>The text does not depend on the order of the city's collections, so two equal cities compare equal.</summary>
        [Test]
        public void ToJson_TheSameCity_GivesTheSameText()
        {
            CityState city = _store.Snapshot!.CopyCity();

            Assert.That(PredictionWatch.ToJson(_store.Snapshot!.CopyCity()), Is.EqualTo(PredictionWatch.ToJson(city)));
            Assert.That(PredictionWatch.ToJson(city), Does.StartWith("{\"clock\":" + city.Clock));
        }
    }
}
