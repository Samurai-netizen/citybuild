#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.Research;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The research screen end to end in a running scene (M0-52), with the real city store, command sender and screen over a transport that
    /// answers when the test says: the screen opens and closes, shows the era's four nodes in two columns with their status and buttons, sends
    /// one <c>StartResearch</c> however often Finance is pressed and shows it as pending, shows the running research when the server answers,
    /// ignores a press on a button that is off, and takes part in the menu group (owner directive OD-16) as <c>GameRoot</c> joins it. The
    /// fixture city is the start of a game: 700 coins, 40 logs, 10 planks, no research.
    /// </summary>
    public sealed class ResearchFlowTests
    {
        /// <summary>The object everything is made under.</summary>
        private GameObject _host = null!;

        /// <summary>The network; answers when the test says.</summary>
        private ControllableTransport _transport = null!;

        /// <summary>The city store the sender hands snapshots to.</summary>
        private ClientCityStore _store = null!;

        /// <summary>The displayed city, which the flow follows.</summary>
        private DisplayCity _display = null!;

        /// <summary>The command sender Finance uses.</summary>
        private CommandSender _sender = null!;

        /// <summary>The screen under test.</summary>
        private ResearchScreen _screen = null!;

        /// <summary>The flow under test.</summary>
        private ResearchFlow _flow = null!;

        /// <summary>The build menu, joined with the screen in one group.</summary>
        private BuildMenu _menu = null!;

        /// <summary>Cancels commands still open at the end of a test.</summary>
        private CancellationTokenSource _cancel = null!;

        /// <summary>Before each test: a client with the start city and a flow over it; waits a frame for the screen's tree.</summary>
        [UnitySetUp]
        public IEnumerator CreateFlow()
        {
            ContentResponseDto contentDto = new ApiJson().Deserialize<ContentResponseDto>(Fixture("content.json"))!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(contentDto));
            Assert.That(built.IsValid, Is.True);
            GameContent content = built.Content!;

            _host = new GameObject("Test research host");
            _store = new ClientCityStore();
            var snapshotDto = new ApiJson().Deserialize<CitySnapshotDto>(Fixture("snapshot-city-start.json"))!;
            Assert.That(_store.Apply(SnapshotMapper.ToSnapshot(snapshotDto).Value!), Is.True);
            _display = new DisplayCity(_store, new DisplayClock(_store, 1), content);
            _transport = new ControllableTransport();
            _sender = new CommandSender(new ApiClient("http://localhost:8080", 30, _transport), _store, CommandPolicy.Default);
            _cancel = new CancellationTokenSource();
            _screen = ResearchScreen.Create(_host.transform)!;
            _flow = new ResearchFlow(content, _sender, _screen, _cancel.Token);
            _menu = BuildMenu.Create(_host.transform, new BuildMenuModel(content, _display.Current!))!;
            var menus = new MenuGroup();
            _menu.Opened += menus.Join(() => _menu.SetOpen(false)).NotifyOpened;
            _screen.Opened += menus.Join(_flow.Close).NotifyOpened;
            _display.Changed += () => _flow.Update(_display.Current!);
            _flow.Update(_display.Current!);
            yield return null;
        }

        /// <summary>After each test: ends open commands and destroys everything.</summary>
        [TearDown]
        public void Destroy()
        {
            _flow.Dispose();
            _cancel.Cancel();
            _cancel.Dispose();
            Object.DestroyImmediate(_host);
        }

        /// <summary>The text of a file in <c>Tests/Fixtures</c>.</summary>
        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures", name));
        }

        /// <summary>The element called <paramref name="name"/> in the screen.</summary>
        private T Q<T>(string name) where T : VisualElement
        {
            return _screen.Root!.Q<T>(name);
        }

        /// <summary>The node elements in the order of the model: sharp_axes, trade_charter, frame_saw, storehouses.</summary>
        private List<VisualElement> Nodes()
        {
            return _screen.Root!.Query<VisualElement>(ResearchScreen.NodeName).ToList();
        }

        /// <summary>The label called <paramref name="name"/> inside the node element <paramref name="node"/>.</summary>
        private static Label In(VisualElement node, string name)
        {
            return node.Q<Label>(name);
        }

        /// <summary>Waits, a frame at a time, until <paramref name="condition"/> holds; fails after a few seconds of frames.</summary>
        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            for (int frame = 0; frame < 300 && !condition(); frame++)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, "the condition did not hold in time");
        }

        /// <summary>The panel starts closed; the button opens it, then closes it; the toggle's text follows.</summary>
        [Test]
        public void TheButtonOpensAndClosesThePanel()
        {
            VisualElement panel = Q<VisualElement>(ResearchScreen.PanelName);
            Assert.That(_screen.IsOpen, Is.False);
            Assert.That(panel.ClassListContains("research-panel--hidden"), Is.True);

            _screen.SetOpen(true);

            Assert.That(_screen.IsOpen, Is.True);
            Assert.That(panel.ClassListContains("research-panel--hidden"), Is.False);
            Assert.That(Q<Button>(ResearchScreen.ToggleName).text, Is.EqualTo("Close"));

            _screen.SetOpen(false);

            Assert.That(panel.ClassListContains("research-panel--hidden"), Is.True);
            Assert.That(Q<Button>(ResearchScreen.ToggleName).text, Is.EqualTo("Research"));
        }

        /// <summary>The header, the greyed-out rule and four nodes in two columns, each with its words.</summary>
        [Test]
        public void ShowsTheEraHeaderTheRuleAndTheNodesInColumns()
        {
            Assert.That(Q<Label>(ResearchScreen.EraName).text, Is.EqualTo("Early Middle Ages — 0 / 4 researched"));
            Assert.That(Q<Label>(ResearchScreen.RuleName).text, Is.EqualTo("Complete all to advance"));
            Assert.That(Q<Label>(ResearchScreen.RuleName).ClassListContains("research-rule--off"), Is.True);
            Assert.That(_screen.Root!.Query<VisualElement>(className: "research-column").ToList().Count, Is.EqualTo(2));
            List<VisualElement> nodes = Nodes();
            Assert.That(nodes.Count, Is.EqualTo(4));
            Assert.That(In(nodes[0], ResearchScreen.NodeNameName).text, Is.EqualTo("Sharpened Axes"));
            Assert.That(In(nodes[0], ResearchScreen.NodeCostName).text, Is.EqualTo("Cost: 250 Coins"));
            Assert.That(In(nodes[0], ResearchScreen.NodeTimeName).text, Is.EqualTo("Takes 30m"));
            Assert.That(In(nodes[0], ResearchScreen.NodeEffectName).text, Is.EqualTo("Woodcutter's Hut: −20% labor"));
            Assert.That(In(nodes[0], ResearchScreen.NodeStatusName).text, Is.EqualTo("Ready to research"));
            Assert.That(nodes[0].parent, Is.SameAs(nodes[1].parent), "the two nodes with no prerequisite share a column");
            Assert.That(nodes[2].parent, Is.SameAs(nodes[3].parent));
            Assert.That(nodes[0].parent, Is.Not.SameAs(nodes[2].parent));
        }

        /// <summary>An available node can be financed; a locked one is off and names what it requires.</summary>
        [Test]
        public void AvailableNodesCanBeFinanced_LockedOnesSayWhatTheyRequire()
        {
            List<VisualElement> nodes = Nodes();

            Assert.That(_screen.FinanceButton("trade_charter")!.enabledSelf, Is.True);
            Assert.That(_screen.FinanceButton("frame_saw")!.enabledSelf, Is.False);
            Assert.That(In(nodes[2], ResearchScreen.NodeStatusName).text, Is.EqualTo("Requires: Sharpened Axes"));
            Assert.That(In(nodes[2], ResearchScreen.NodeNoteName).text, Is.EqualTo("Requires: Sharpened Axes"));
            Assert.That(nodes[2].ClassListContains("research-node--locked"), Is.True);
        }

        /// <summary>A press on a button that is off sends nothing.</summary>
        [Test]
        public void PressingALockedNode_SendsNothing()
        {
            Assert.That(_screen.PressFinance("frame_saw"), Is.False);
            Assert.That(_screen.PressFinance("nonsense"), Is.False);
            Assert.That(_sender.Pending.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Finance sends one StartResearch however often it is pressed and shows it as pending at once (every button off); when the server
        /// answers, the node runs and the others say another research is running.
        /// </summary>
        [UnityTest]
        public IEnumerator Finance_SendsOnce_ShowsPending_AndThenTheRunningResearch()
        {
            Assert.That(_screen.PressFinance("trade_charter"), Is.True);
            Assert.That(_screen.PressFinance("trade_charter"), Is.False, "the second press finds the button off");

            Assert.That(_sender.Pending.Count, Is.EqualTo(1));
            Assert.That(_sender.Pending[0].Intent.Kind, Is.EqualTo(CommandKind.StartResearch));
            Assert.That(_sender.Pending[0].Intent.ContentId, Is.EqualTo("trade_charter"));
            List<VisualElement> nodes = Nodes();
            Assert.That(_screen.FinanceButton("trade_charter")!.enabledSelf, Is.False);
            Assert.That(In(nodes[1], ResearchScreen.NodeNoteName).text, Is.EqualTo("Starting research…"));
            Assert.That(In(nodes[0], ResearchScreen.NodeNoteName).text, Is.EqualTo("Another research is running"));
            yield return WaitUntil(() => _transport.OpenCount == 1);
            Assert.That(_transport.Requests.Count, Is.EqualTo(1));
            Assert.That(_transport.Requests[0].JsonBody, Does.Contain("\"researchId\":\"trade_charter\""));

            _transport.Respond(200, Fixture("snapshot-command-start-research.json"));
            yield return WaitUntil(() => _sender.Pending.Count == 0);

            nodes = Nodes();
            Assert.That(In(nodes[1], ResearchScreen.NodeStatusName).text, Does.StartWith("In progress"));
            Assert.That(nodes[1].ClassListContains("research-node--running"), Is.True);
            Assert.That(_screen.FinanceButton("trade_charter")!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.FinanceButton("sharp_axes")!.enabledSelf, Is.False);
            Assert.That(In(nodes[0], ResearchScreen.NodeNoteName).text, Is.EqualTo("Another research is running"));
        }

        /// <summary>When the server refuses the start, the buttons work again.</summary>
        [UnityTest]
        public IEnumerator ARefusedStart_LeavesTheButtonsWorking()
        {
            _screen.PressFinance("sharp_axes");
            yield return WaitUntil(() => _transport.OpenCount == 1);

            _transport.Respond(409, Fixture("error-409-RESEARCH_IN_PROGRESS.json"));
            yield return WaitUntil(() => _sender.Pending.Count == 0);

            Assert.That(_screen.FinanceButton("sharp_axes")!.enabledSelf, Is.True);
        }

        /// <summary>Opening the build menu's sheet closes the research screen, and opening the screen closes the sheet.</summary>
        [Test]
        public void TheScreenAndTheBuildSheet_CloseEachOther()
        {
            _screen.SetOpen(true);

            _menu.SetOpen(true);

            Assert.That(_menu.IsOpen, Is.True);
            Assert.That(_screen.IsOpen, Is.False);

            _screen.SetOpen(true);

            Assert.That(_screen.IsOpen, Is.True);
            Assert.That(_menu.IsOpen, Is.False);
        }

        /// <summary>A refresh with the panel already open is not an opening: it does not close the other menus again.</summary>
        [Test]
        public void ARefreshIsNotAnOpening()
        {
            int opened = 0;
            _screen.Opened += () => opened++;
            _screen.SetOpen(true);
            _screen.SetOpen(true);
            _flow.Update(_display.Current!);

            Assert.That(opened, Is.EqualTo(1));
        }

        /// <summary>With the panel closed only the button takes the pointer, so a press in the middle of the screen belongs to the ground.</summary>
        [Test]
        public void AClosedScreen_LeavesTheMiddleOfTheScreenToTheGround()
        {
            var middle = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Assert.That(_screen.IsOverUi(middle), Is.False);
        }

        /// <summary>A flow that was disposed no longer reacts to Finance.</summary>
        [Test]
        public void AfterDispose_TheFlowIgnoresFinance()
        {
            _flow.Dispose();

            _screen.PressFinance("sharp_axes");

            Assert.That(_sender.Pending.Count, Is.EqualTo(0));
        }
    }
}
