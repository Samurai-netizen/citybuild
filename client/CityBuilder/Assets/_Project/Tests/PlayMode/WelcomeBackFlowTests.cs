#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Presentation.WelcomeBack;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The welcome-back report in a running scene (M0-53), with the real city store, flow and panel: the snapshot the store already holds when the
    /// flow is made is reported (the startup answer), a long settlement opens the panel and a routine one never does, Continue closes it, one
    /// snapshot is reported once, a newer long settlement replaces an open report, the report takes part in the menu group (owner directive OD-16)
    /// as <c>GameRoot</c> joins it, and while it is open it takes the pointer.
    /// </summary>
    public sealed class WelcomeBackFlowTests
    {
        /// <summary>The object everything is made under.</summary>
        private GameObject _host = null!;

        /// <summary>The game content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>The city store the flow follows.</summary>
        private ClientCityStore _store = null!;

        /// <summary>The panel under test.</summary>
        private WelcomeBackPanel _panel = null!;

        /// <summary>The flow under test.</summary>
        private WelcomeBackFlow _flow = null!;

        /// <summary>Before each test: the content and a host object; the store, panel and flow are made by <see cref="Start"/>, because what the store holds first matters.</summary>
        [SetUp]
        public void LoadContent()
        {
            ContentResponseDto contentDto = new ApiJson().Deserialize<ContentResponseDto>(Fixture("content.json"))!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(contentDto));
            Assert.That(built.IsValid, Is.True);
            _content = built.Content!;
            _host = new GameObject("Test welcome-back host");
            _store = new ClientCityStore();
        }

        /// <summary>After each test: stops the flow and destroys everything.</summary>
        [TearDown]
        public void Destroy()
        {
            _flow?.Dispose();
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

        /// <summary>A newer snapshot of the same city carrying a settlement of <paramref name="elapsed"/> seconds that produced 5 logs.</summary>
        private static CitySnapshot Newer(long version, long elapsed, string basis = "snapshot-city-start.json")
        {
            CitySnapshot start = Snapshot(basis);
            var settlement = new SettlementSummary(
                elapsed,
                elapsed,
                new[] { new KeyValuePair<ItemId, long>(new ItemId("logs"), 5) },
                new KeyValuePair<ItemId, long>[0],
                new int[0],
                new ResearchId[0],
                false);
            return new CitySnapshot(version, start.ContentVersion, start.CopyCity(), settlement);
        }

        /// <summary>Makes the panel and the flow over a store that holds <paramref name="held"/> (or nothing) and waits a frame for the panel's tree.</summary>
        private IEnumerator Start(CitySnapshot? held)
        {
            if (held != null)
            {
                _store.Apply(held);
            }

            _panel = WelcomeBackPanel.Create(_host.transform)!;
            _flow = new WelcomeBackFlow(_content, _store, _panel);
            yield return null;
        }

        /// <summary>The label called <paramref name="name"/> in the panel.</summary>
        private Label Label(string name)
        {
            return _panel.Root!.Q<Label>(name);
        }

        /// <summary>The texts of the labels of the class <paramref name="className"/> in the panel, top to bottom.</summary>
        private string[] Texts(string className)
        {
            return _panel.Root!.Query<Label>(className: className).ToList().Select(label => label.text).ToArray();
        }

        /// <summary>Whether the scrim is shown.</summary>
        private bool ScrimShown()
        {
            return !_panel.Root!.Q<VisualElement>(WelcomeBackPanel.ScrimName).ClassListContains("welcome-scrim--hidden");
        }

        /// <summary>The snapshot the store held before the flow existed (the startup answer) is reported: the panel opens with the real numbers.</summary>
        [UnityTest]
        public IEnumerator TheSnapshotHeldAtStart_IsReported()
        {
            yield return Start(Snapshot("snapshot-city-research-completed.json"));

            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(ScrimShown(), Is.True);
            Assert.That(Label(WelcomeBackPanel.TitleName).text, Is.EqualTo("While you were away (7 m)"));
            Assert.That(Texts(WelcomeBackPanel.HeadingClass), Is.EqualTo(new[] { "Produced", "Used", "Research" }));
            Assert.That(
                Texts(WelcomeBackPanel.LineClass),
                Is.EqualTo(new[] { "14 Logs", "7 Planks", "14 Logs", "Trade Charter — Unlocks Trading Post" }));
            Assert.That(Label(WelcomeBackPanel.CapNoteName).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(Label(WelcomeBackPanel.EmptyName).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        /// <summary>A routine settlement held at the start is not reported: the panel stays closed.</summary>
        [UnityTest]
        public IEnumerator ARoutineSnapshotHeldAtStart_IsNotReported()
        {
            yield return Start(Snapshot("snapshot-city-settled-buildings.json"));

            Assert.That(_panel.IsOpen, Is.False);
            Assert.That(ScrimShown(), Is.False);
            Assert.That(_panel.Model, Is.Null);
        }

        /// <summary>An empty store reports nothing; a routine poll afterwards reports nothing; a long one opens the panel.</summary>
        [UnityTest]
        public IEnumerator Polls_OnlyALongSettlementOpensThePanel()
        {
            yield return Start(null);
            Assert.That(_panel.IsOpen, Is.False);

            _store.Apply(Newer(10, 120));
            yield return null;
            Assert.That(_panel.IsOpen, Is.False);

            _store.Apply(Newer(11, 299));
            yield return null;
            Assert.That(_panel.IsOpen, Is.False);

            _store.Apply(Newer(12, 300));
            yield return null;
            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(Label(WelcomeBackPanel.TitleName).text, Is.EqualTo("While you were away (5 m)"));
            Assert.That(Texts(WelcomeBackPanel.LineClass), Is.EqualTo(new[] { "5 Logs" }));
        }

        /// <summary>Continue closes the report and says so; a second press changes nothing.</summary>
        [UnityTest]
        public IEnumerator Continue_ClosesTheReport()
        {
            yield return Start(Snapshot("snapshot-city-research-completed.json"));
            int dismissed = 0;
            _panel.Dismissed += () => dismissed++;

            _panel.PressContinue();
            _panel.PressContinue();
            yield return null;

            Assert.That(_panel.IsOpen, Is.False);
            Assert.That(ScrimShown(), Is.False);
            Assert.That(dismissed, Is.EqualTo(1));
        }

        /// <summary>The Continue button of the layout closes the report as well, not only the call.</summary>
        [UnityTest]
        public IEnumerator TheContinueButton_ClosesTheReport()
        {
            yield return Start(Snapshot("snapshot-city-research-completed.json"));

            // A button's Clickable answers the submit event (what a keyboard or a gamepad sends), which a test can send without a pointer.
            Button button = _panel.Root!.Q<Button>(WelcomeBackPanel.ContinueName);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }

            yield return null;
            Assert.That(_panel.IsOpen, Is.False);
        }

        /// <summary>A snapshot is reported once: the same snapshot taken again, after Continue, does not bring the report back.</summary>
        [UnityTest]
        public IEnumerator OneSnapshot_IsReportedOnce()
        {
            CitySnapshot held = Snapshot("snapshot-city-research-completed.json");
            yield return Start(held);
            _panel.PressContinue();

            _store.Replace(held);
            yield return null;

            Assert.That(_panel.IsOpen, Is.False);
        }

        /// <summary>A newer long settlement while a report is open replaces its words and does not announce an opening twice.</summary>
        [UnityTest]
        public IEnumerator ANewerReport_ReplacesTheOpenOne()
        {
            yield return Start(Snapshot("snapshot-city-research-completed.json"));
            int opened = 0;
            _panel.Opened += () => opened++;

            _store.Apply(Newer(40, 3600, "snapshot-city-research-completed.json"));
            yield return null;

            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(opened, Is.EqualTo(0));
            Assert.That(Label(WelcomeBackPanel.TitleName).text, Is.EqualTo("While you were away (1 h)"));
            Assert.That(Texts(WelcomeBackPanel.LineClass), Is.EqualTo(new[] { "5 Logs" }));
        }

        /// <summary>A long absence in which nothing happened still gets a report, saying so.</summary>
        [UnityTest]
        public IEnumerator AnEmptyReport_SaysNothingHappened()
        {
            yield return Start(null);
            var settlement = new SettlementSummary(
                7200, 7200, new KeyValuePair<ItemId, long>[0], new KeyValuePair<ItemId, long>[0], new int[0], new ResearchId[0], false);
            CitySnapshot start = Snapshot("snapshot-city-start.json");

            _store.Apply(new CitySnapshot(10, start.ContentVersion, start.CopyCity(), settlement));
            yield return null;

            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(Label(WelcomeBackPanel.EmptyName).text, Is.EqualTo(WelcomeBackModel.NothingHappenedText));
            Assert.That(Label(WelcomeBackPanel.EmptyName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Texts(WelcomeBackPanel.HeadingClass), Is.Empty);
        }

        /// <summary>When production hit the cap, the note is shown under the sections.</summary>
        [UnityTest]
        public IEnumerator TheCapNote_IsShownWhenProductionWasCut()
        {
            yield return Start(null);
            var settlement = new SettlementSummary(
                36_000, 28_800, new KeyValuePair<ItemId, long>[0], new KeyValuePair<ItemId, long>[0], new int[0], new ResearchId[0], false);
            CitySnapshot start = Snapshot("snapshot-city-start.json");

            _store.Apply(new CitySnapshot(10, start.ContentVersion, start.CopyCity(), settlement));
            yield return null;

            Assert.That(Label(WelcomeBackPanel.CapNoteName).text, Is.EqualTo("Production paused after 8 h. Check in more often or research storage."));
            Assert.That(Label(WelcomeBackPanel.CapNoteName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        /// <summary>While the report is open the whole screen is the UI's; when closed, the middle of the screen is the ground's.</summary>
        [UnityTest]
        public IEnumerator WhileOpen_TheScrimTakesThePointer()
        {
            yield return Start(Snapshot("snapshot-city-research-completed.json"));
            yield return null;
            yield return null;
            var middle = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var corner = new Vector2(3f, 3f);

            Assert.That(_panel.IsOverUi(middle), Is.True);
            Assert.That(_panel.IsOverUi(corner), Is.True);

            _panel.Dismiss();
            yield return null;
            Assert.That(_panel.IsOverUi(middle), Is.False);
        }

        /// <summary>The report is in the menu group as <c>GameRoot</c> joins it: opening it closes the build menu, and opening the build menu closes it.</summary>
        [UnityTest]
        public IEnumerator InTheMenuGroup_TheReportAndTheBuildMenuCloseEachOther()
        {
            CitySnapshot start = Snapshot("snapshot-city-start.json");
            _store.Apply(start);
            BuildMenu menu = BuildMenu.Create(_host.transform, new BuildMenuModel(_content, start.CopyCity()))!;
            _panel = WelcomeBackPanel.Create(_host.transform)!;
            var menus = new MenuGroup();
            menu.Opened += menus.Join(() => menu.SetOpen(false)).NotifyOpened;
            _panel.Opened += menus.Join(_panel.Dismiss).NotifyOpened;
            _flow = new WelcomeBackFlow(_content, _store, _panel);
            yield return null;
            menu.SetOpen(true);
            Assert.That(_panel.IsOpen, Is.False);

            _store.Apply(Newer(30, 600));
            yield return null;
            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(menu.IsOpen, Is.False);

            menu.SetOpen(true);
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(_panel.IsOpen, Is.False);
        }

        /// <summary>A flow that was disposed no longer reacts to the store.</summary>
        [UnityTest]
        public IEnumerator AfterDispose_TheFlowIgnoresTheStore()
        {
            yield return Start(null);
            _flow.Dispose();
            _flow.Dispose();

            _store.Apply(Newer(10, 600));
            yield return null;

            Assert.That(_panel.IsOpen, Is.False);
        }
    }
}
