#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.WelcomeBack;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The Re-energize couriers button of the welcome-back report (CR-0008-T2 part E), with the real store, panel, flow and a command sender over a
    /// controllable transport: the button shows only when a courier spent stamina, it sends <c>reenergize-couriers</c>, waits (a second press sends
    /// nothing), and success closes the report while a refusal leaves it open.
    /// </summary>
    public sealed class WelcomeBackReenergizeTests
    {
        /// <summary>The object everything is made under.</summary>
        private GameObject _host = null!;

        /// <summary>The store.</summary>
        private ClientCityStore _store = null!;

        /// <summary>The transport the test answers by hand.</summary>
        private ControllableTransport _transport = null!;

        /// <summary>The panel under test.</summary>
        private WelcomeBackPanel _panel = null!;

        /// <summary>The flow under test.</summary>
        private WelcomeBackFlow _flow = null!;

        /// <summary>Cancels commands still open at the end of a test.</summary>
        private CancellationTokenSource _cancel = null!;

        /// <summary>Before each test: the content, the store, a sender and the panel and flow over a report with a tired courier.</summary>
        [UnitySetUp]
        public IEnumerator Create()
        {
            ContentResponseDto contentDto = new ApiJson().Deserialize<ContentResponseDto>(Fixture("content.json"))!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(contentDto));
            Assert.That(built.IsValid, Is.True);
            _host = new GameObject("Test welcome-back host");
            _store = new ClientCityStore();
            CitySnapshot start = Snapshot("snapshot-city-start.json");
            var settlement = new SettlementSummary(
                7200, new KeyValuePair<ItemId, long>[0], new KeyValuePair<ItemId, long>[0], 3600,
                new[] { new TiredCourier(1, 1, start.Clock - 600) }, new int[0], new ResearchId[0], false);
            _store.Apply(new CitySnapshot(start.StateVersion + 1, start.ContentVersion, start.CopyCity(), settlement));
            _transport = new ControllableTransport();
            var sender = new CommandSender(new ApiClient("http://localhost:8080", 30, _transport, allowInsecureHttp: true), _store, CommandPolicy.Default);
            _cancel = new CancellationTokenSource();
            _panel = WelcomeBackPanel.Create(_host.transform)!;
            _flow = new WelcomeBackFlow(built.Content!, _store, _panel, 1, sender, _cancel.Token);
            yield return null;
        }

        /// <summary>After each test: ends open commands and destroys everything.</summary>
        [TearDown]
        public void Destroy()
        {
            _cancel.Cancel();
            _flow.Dispose();
            Object.DestroyImmediate(_host);
        }

        /// <summary>The text of a file in <c>Tests/Fixtures</c>.</summary>
        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures", name));
        }

        /// <summary>The snapshot in the fixture file <paramref name="name"/>.</summary>
        private static CitySnapshot Snapshot(string name)
        {
            var dto = new ApiJson().Deserialize<CitySnapshotDto>(Fixture(name))!;
            ApiResult<CitySnapshot> mapped = SnapshotMapper.ToSnapshot(dto);
            Assert.That(mapped.IsSuccess, Is.True);
            return mapped.Value!;
        }

        /// <summary>The report offers the button, and it is shown in the layout.</summary>
        [UnityTest]
        public IEnumerator TheReport_OffersReenergizeWhenACourierSpentStamina()
        {
            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(_panel.ShowsReenergize, Is.True);
            Button button = _panel.Root!.Q<Button>(WelcomeBackPanel.ReenergizeName);
            Assert.That(button.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            yield return null;
        }

        /// <summary>Pressing it sends the command once, however often it is pressed while waiting; success closes the report.</summary>
        [UnityTest]
        public IEnumerator Reenergize_SendsOnceAndSuccessClosesTheReport()
        {
            _panel.PressReenergize();
            _panel.PressReenergize();
            yield return null;

            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_transport.Requests[0].Url, Does.EndWith("/commands/reenergize-couriers"));
            Assert.That(_panel.ReenergizeBusy, Is.True);

            _transport.Respond(200, Fixture("snapshot-command-place-building.json"));
            yield return null;
            yield return null;

            Assert.That(_panel.IsOpen, Is.False);
            Assert.That(_panel.ReenergizeBusy, Is.False);
        }

        /// <summary>A refusal leaves the report open and the button usable again.</summary>
        [UnityTest]
        public IEnumerator Reenergize_RefusedLeavesTheReportOpen()
        {
            _panel.PressReenergize();
            yield return null;

            _transport.Respond(409, Fixture("error-409-TILE_OCCUPIED.json"));
            yield return null;
            yield return null;

            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(_panel.ReenergizeBusy, Is.False);
        }
    }
}
