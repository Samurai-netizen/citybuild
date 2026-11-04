#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Inspector;
using CityBuilder.Client.Presentation.Ui;
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
    /// The building inspector end to end in a running scene (M0-51), with the real world, city store, command sender and screen over a
    /// transport that answers when the test says: tap a building and see its words, press − and + and see the target pending, then sent once
    /// after the debounce, press Upgrade twice and see one request, see the disabled reasons, and see the placement flow keep the ground to itself.
    /// With the build menu joined to the inspector in one <see cref="MenuGroup"/>, as <c>GameRoot</c> does (owner directive OD-16), opening either closes the other.
    /// The test city (<see cref="InspectorCity"/>) has two cottages (tiles 0 and 1), a sawmill with 3 of 3 workers at tile 2 and a woodcutter
    /// with 2 of 2 at tile 4, with 220 coins, 10 logs and 10 planks in the sawmill's store and 3 free workers.
    /// </summary>
    public sealed class InspectorFlowTests
    {
        /// <summary>The sawmill's building id in the test city.</summary>
        private const int SawmillId = 3;

        /// <summary>The object everything is made under.</summary>
        private GameObject _host = null!;

        /// <summary>The city's ground and tile selection.</summary>
        private CityWorld _world = null!;

        /// <summary>The network; answers when the test says.</summary>
        private ControllableTransport _transport = null!;

        /// <summary>The city store the sender hands snapshots to.</summary>
        private ClientCityStore _store = null!;

        /// <summary>The displayed city, which the flow follows.</summary>
        private DisplayCity _display = null!;

        /// <summary>The command sender the inspector's buttons use.</summary>
        private CommandSender _sender = null!;

        /// <summary>The placement flow's state, which keeps the inspector closed while it has something in progress.</summary>
        private PlacementFlowModel _placement = null!;

        /// <summary>The build menu, joined to the inspector in one group.</summary>
        private BuildMenu _menu = null!;

        /// <summary>The screen under test.</summary>
        private BuildingInspector _inspector = null!;

        /// <summary>The flow under test.</summary>
        private InspectorFlow _flow = null!;

        /// <summary>Cancels commands still open at the end of a test.</summary>
        private CancellationTokenSource _cancel = null!;

        /// <summary>Before each test: a client with the inspector's city, an 8 by 8 grid and a flow over it; waits a frame for the screen's tree.</summary>
        [UnitySetUp]
        public IEnumerator CreateFlow()
        {
            yield return CreateFlowFor(InspectorCity(220, 3, 243, 10));
        }

        /// <summary>
        /// The city the tests inspect, as an answer's JSON: the real producing answer (its clock 196, era, content) with the buildings these tests
        /// need, which no captured answer has since CR-0008 changed the start: cottages 1 and 2 on (0, 0) and (1, 0), the sawmill 3 on (2, 0)
        /// producing with <paramref name="sawmillWorkers"/> workers from second 183 to <paramref name="sawmillEnds"/> (180 labor) with logs
        /// and planks in its store, and a woodcutter 4 on (4, 0) with 2 of 2 workers. <paramref name="purse"/> coins; no goods elsewhere.
        /// </summary>
        private static string InspectorCity(long purse, int sawmillWorkers, long sawmillEnds, long stateVersion)
        {
            var json = new ApiJson();
            CitySnapshotDto real = json.Deserialize<CitySnapshotDto>(Fixture("snapshot-city-producing.json"))!;
            SnapshotCityDto city = real.City;
            var none = new Dictionary<string, long>();
            var buildings = new List<SnapshotBuildingDto>
            {
                new SnapshotBuildingDto(1, "house", 0, 0, 1, 0, 0, BuildingPhaseDto.Idle, 0, null, 0, 0, 0, "NoRecipe", none, none, none, null),
                new SnapshotBuildingDto(2, "house", 1, 0, 1, 0, 0, BuildingPhaseDto.Idle, 0, null, 0, 0, 0, "NoRecipe", none, none, none, null),
                new SnapshotBuildingDto(
                    3, "sawmill", 2, 0, 1, sawmillWorkers, 3, BuildingPhaseDto.Producing, 183, sawmillEnds, 180, 180, 183, null,
                    new Dictionary<string, long> { ["logs"] = 10, ["planks"] = 10 }, new Dictionary<string, long> { ["logs"] = 40, ["planks"] = 20 }, none, null),
                new SnapshotBuildingDto(
                    4, "woodcutter", 4, 0, 1, 2, 2, BuildingPhaseDto.Producing, 183, 213, 60, 60, 183, null, none,
                    new Dictionary<string, long> { ["logs"] = 40 }, none, null),
            };
            long assigned = sawmillWorkers + 2;
            var cityDto = new SnapshotCityDto(
                city.Clock,
                city.Era,
                purse,
                new Dictionary<string, long> { ["coins"] = purse, ["logs"] = 10, ["planks"] = 10 },
                new SnapshotWorkersDto(8, assigned, 0, 8 - assigned),
                new SnapshotBuildersDto(city.Builders.Slots, 0),
                new SnapshotResearchDto(null, null, null, none, new List<string>()),
                buildings,
                new List<SnapshotRoadDto>(),
                new List<SnapshotTripDto>());
            return json.Serialize(new CitySnapshotDto(real.RequestId, stateVersion, real.ServerTimeUtc, real.ContentVersion, cityDto, real.Settlement));
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

        /// <summary>Makes the client for the city in the snapshot text <paramref name="snapshotJson"/>.</summary>
        private IEnumerator CreateFlowFor(string snapshotJson)
        {
            ContentResponseDto contentDto = new ApiJson().Deserialize<ContentResponseDto>(Fixture("content.json"))!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(contentDto));
            Assert.That(built.IsValid, Is.True);
            GameContent content = built.Content!;

            _host = new GameObject("Test inspector host");
            _world = _host.AddComponent<CityWorld>();
            var grid = new CityGrid(content.Rules.GridWidth, content.Rules.GridHeight);
            _world.ShowGrid(grid);

            _store = new ClientCityStore();
            var snapshotDto = new ApiJson().Deserialize<CitySnapshotDto>(snapshotJson)!;
            Assert.That(_store.Apply(SnapshotMapper.ToSnapshot(snapshotDto).Value!), Is.True);
            _display = new DisplayCity(_store, new DisplayClock(_store, 1), content);

            _transport = new ControllableTransport();
            _sender = new CommandSender(new ApiClient("http://localhost:8080", 30, _transport, allowInsecureHttp: true), _store, CommandPolicy.Default);
            _placement = new PlacementFlowModel(content, grid);
            _placement.Update(_display.Current!);
            _cancel = new CancellationTokenSource();
            _inspector = BuildingInspector.Create(_host.transform)!;
            _flow = new InspectorFlow(content, _world, _sender, _inspector, _placement, _cancel.Token);
            _menu = BuildMenu.Create(_host.transform, new BuildMenuModel(content, _display.Current!))!;
            var menus = new MenuGroup();
            _menu.Opened += menus.Join(() => _menu.SetOpen(false)).NotifyOpened;
            _inspector.Opened += menus.Join(_flow.Close).NotifyOpened;
            _display.Changed += () => _flow.Update(_display.Current!);
            _flow.Update(_display.Current!);
            yield return null;
        }

        /// <summary>The text of a file in <c>Tests/Fixtures</c>.</summary>
        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures", name));
        }

        /// <summary>The screen's label called <paramref name="name"/>.</summary>
        private Label LabelOf(string name)
        {
            return _inspector.Root!.Q<Label>(name);
        }

        /// <summary>The screen's button called <paramref name="name"/>.</summary>
        private Button ButtonOf(string name)
        {
            return _inspector.Root!.Q<Button>(name);
        }

        /// <summary>The player taps the tile (x, y) on the ground.</summary>
        private void TapTile(int x, int y)
        {
            _world.Selection!.Tap(new GridCoordinate(x, y));
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

        /// <summary>Tapping a building opens the inspector with its name, level, status, recipe, rates and stepper value.</summary>
        [Test]
        public void TappingABuilding_ShowsItsInspector()
        {
            TapTile(2, 0);

            Assert.That(_inspector.IsShown, Is.True);
            Assert.That(LabelOf(BuildingInspector.NameName).text, Is.EqualTo("Sawmill"));
            Assert.That(LabelOf(BuildingInspector.LevelName).text, Is.EqualTo("Level 1 of 2"));
            Assert.That(LabelOf(BuildingInspector.StatusName).text, Is.EqualTo("Producing"));
            Assert.That(LabelOf(BuildingInspector.RecipeName).text, Is.EqualTo("2 Logs → 1 Planks"));
            Assert.That(LabelOf(BuildingInspector.RatesName).text, Is.EqualTo("Uses 120 Logs/h · Makes 60 Planks/h"));
            Assert.That(LabelOf(BuildingInspector.WorkersName).text, Is.EqualTo("3 / 3"));
            Assert.That(_inspector.Root!.Q<VisualElement>(BuildingInspector.StepperName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        /// <summary>A tile with no building shows nothing; a cottage shows its inspector without the stepper.</summary>
        [Test]
        public void AnEmptyTileShowsNothing_AndACottageHasNoStepper()
        {
            TapTile(3, 0);
            Assert.That(_inspector.IsShown, Is.False);

            TapTile(0, 0);

            Assert.That(_inspector.IsShown, Is.True);
            Assert.That(LabelOf(BuildingInspector.NameName).text, Is.EqualTo("Cottage"));
            Assert.That(LabelOf(BuildingInspector.RecipeName).text, Is.EqualTo("Provides 4 workers"));
            Assert.That(_inspector.Root!.Q<VisualElement>(BuildingInspector.StepperName).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        /// <summary>Tapping the same tile again, or pressing close, hides the inspector and drops the tile.</summary>
        [Test]
        public void TappingTheTileAgain_OrPressingClose_HidesIt()
        {
            TapTile(2, 0);
            TapTile(2, 0);
            Assert.That(_inspector.IsShown, Is.False);

            TapTile(2, 0);
            Assert.That(_inspector.IsShown, Is.True);
            _inspector.PressClose();

            Assert.That(_inspector.IsShown, Is.False);
            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>While the placement flow has a card picked the tile belongs to it: no inspector, and it comes back when the flow ends.</summary>
        [Test]
        public void WhilePlacing_TheInspectorStaysClosed()
        {
            _placement.SetType(new BuildingTypeId("house"));
            TapTile(2, 0);

            Assert.That(_inspector.IsShown, Is.False);

            _placement.Cancel();

            Assert.That(_inspector.IsShown, Is.True);
        }

        /// <summary>The panel takes the pointer only while it is shown, so the ground still pans and taps around a hidden one.</summary>
        [Test]
        public void AHiddenInspector_IsNotOverUi()
        {
            Assert.That(_inspector.IsOverUi(new Vector2(Screen.width - 10, Screen.height / 2f)), Is.False);
        }

        /// <summary>
        /// − shows the lower target as pending at once (the value, the note and the rate preview), a second tap lowers it again, and one request
        /// with the last absolute target goes out after the debounce; nothing changes in the city until the server answers, and then it does.
        /// </summary>
        [UnityTest]
        public IEnumerator Minus_ShowsThePendingTarget_AndSendsTheLastOneOnce()
        {
            TapTile(2, 0);

            _inspector.PressMinus();

            Assert.That(_sender.Pending.Count, Is.EqualTo(1));
            Assert.That(_sender.Pending[0].Intent.Kind, Is.EqualTo(CommandKind.AssignWorkers));
            Assert.That(_sender.Pending[0].Intent.BuildingId, Is.EqualTo(SawmillId));
            Assert.That(_sender.Pending[0].Intent.Workers, Is.EqualTo(2));
            Assert.That(LabelOf(BuildingInspector.WorkersName).text, Is.EqualTo("2 / 3"));
            Assert.That(LabelOf(BuildingInspector.StepperNoteName).text, Is.EqualTo("Updating workers…"));
            Assert.That(LabelOf(BuildingInspector.PreviewName).text, Is.EqualTo("With 2 workers: Uses 80 Logs/h · Makes 40 Planks/h"));
            Assert.That(LabelOf(BuildingInspector.RatesName).text, Is.EqualTo("Uses 120 Logs/h · Makes 60 Planks/h"), "the city has not changed");
            Assert.That(_transport.OpenCount, Is.EqualTo(0), "nothing is sent before the debounce has passed");

            _inspector.PressMinus();

            Assert.That(_sender.Pending.Count, Is.EqualTo(1), "the second target replaced the first");
            Assert.That(_sender.Pending[0].Intent.Workers, Is.EqualTo(1));
            Assert.That(LabelOf(BuildingInspector.WorkersName).text, Is.EqualTo("1 / 3"));

            yield return new WaitForSeconds(0.8f);

            Assert.That(_transport.Requests.Count, Is.EqualTo(1));
            Assert.That(_transport.Requests[0].JsonBody, Does.Contain("\"workers\":1"));
            Assert.That(_transport.Requests[0].JsonBody, Does.Contain("\"buildingId\":" + SawmillId));

            // One worker does the 180 labor left at second 183 in 180 s, so the cycle ends at 363: the engine refuses a city whose end is another.
            string answer = InspectorCity(220, 1, 363, 11);
            _transport.Respond(200, answer);
            yield return WaitUntil(() => _sender.Pending.Count == 0);

            Assert.That(LabelOf(BuildingInspector.WorkersName).text, Is.EqualTo("1 / 3"));
            Assert.That(LabelOf(BuildingInspector.StepperNoteName).text, Is.Empty, "no longer pending, and + works again");
            Assert.That(LabelOf(BuildingInspector.PreviewName).text, Is.Empty);
            Assert.That(_inspector.Model!.PlusEnabled, Is.True);
        }

        /// <summary>+ at the level's most workers does nothing: no command is made.</summary>
        [Test]
        public void Plus_AtTheMaximum_SendsNothing()
        {
            TapTile(2, 0);

            _inspector.PressPlus();

            Assert.That(_sender.Pending.Count, Is.EqualTo(0));
            Assert.That(_inspector.Model!.PlusEnabled, Is.False);
        }

        /// <summary>+ raises the pending target by one while the city has free workers (the woodcutter has 2 of 2: use the sawmill after a −).</summary>
        [Test]
        public void Plus_AfterMinus_RaisesThePendingTarget()
        {
            TapTile(2, 0);
            _inspector.PressMinus();
            _inspector.PressMinus();

            _inspector.PressPlus();

            Assert.That(_sender.Pending.Count, Is.EqualTo(1));
            Assert.That(_sender.Pending[0].Intent.Workers, Is.EqualTo(2));
            Assert.That(LabelOf(BuildingInspector.WorkersName).text, Is.EqualTo("2 / 3"));
        }

        /// <summary>The test city cannot pay the sawmill's upgrade coins: the section says what is missing, and pressing the button sends nothing.</summary>
        [Test]
        public void Upgrade_WhenUnaffordable_IsOffWithItsReason()
        {
            TapTile(2, 0);

            Assert.That(LabelOf(BuildingInspector.UpgradeTitleName).text, Is.EqualTo("Upgrade to level 2"));
            Assert.That(LabelOf(BuildingInspector.UpgradeCostName).text, Is.EqualTo("Cost: 250 Coins, 30 Planks"));
            Assert.That(LabelOf(BuildingInspector.UpgradeTimeName).text, Is.EqualTo("Takes 3m"));
            Assert.That(LabelOf(BuildingInspector.UpgradeGainName).text, Is.EqualTo("Max workers 3 → 6"));
            Assert.That(LabelOf(BuildingInspector.UpgradeNoteName).text, Is.EqualTo("Not enough Coins"), "only the coins are paid at once (CR-0008)");
            Assert.That(ButtonOf(BuildingInspector.UpgradeName).enabledSelf, Is.False);

            _inspector.PressUpgrade();

            Assert.That(_sender.Pending.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// With the coins in the purse Upgrade sends one request however many times it is pressed, shows that it is requested, and works again
        /// when the server refuses it.
        /// </summary>
        [UnityTest]
        public IEnumerator Upgrade_PressedTwice_SendsOnce_AndComesBackAfterARefusal()
        {
            _flow.Dispose();
            _cancel.Cancel();
            Object.DestroyImmediate(_host);
            yield return CreateFlowFor(InspectorCity(700, 3, 243, 10));
            TapTile(2, 0);
            Assert.That(ButtonOf(BuildingInspector.UpgradeName).enabledSelf, Is.True);

            _inspector.PressUpgrade();
            _inspector.PressUpgrade();

            Assert.That(_sender.Pending.Count, Is.EqualTo(1));
            Assert.That(_sender.Pending[0].Intent.Kind, Is.EqualTo(CommandKind.UpgradeBuilding));
            Assert.That(_sender.Pending[0].Intent.BuildingId, Is.EqualTo(SawmillId));
            Assert.That(ButtonOf(BuildingInspector.UpgradeName).enabledSelf, Is.False);
            Assert.That(LabelOf(BuildingInspector.UpgradeNoteName).text, Is.EqualTo("Upgrade requested…"));
            yield return WaitUntil(() => _transport.OpenCount == 1);
            Assert.That(_transport.Requests.Count, Is.EqualTo(1));

            _transport.Respond(409, Fixture("error-409-NO_FREE_BUILDER.json"));
            yield return WaitUntil(() => _sender.Pending.Count == 0);

            Assert.That(ButtonOf(BuildingInspector.UpgradeName).enabledSelf, Is.True);
            Assert.That(LabelOf(BuildingInspector.UpgradeNoteName).text, Is.Empty);
        }

        /// <summary>Opening the build menu's sheet closes the inspector and drops its tile.</summary>
        [Test]
        public void OpeningTheBuildSheet_ClosesTheInspector()
        {
            TapTile(2, 0);
            Assert.That(_inspector.IsShown, Is.True);

            _menu.SetOpen(true);

            Assert.That(_menu.IsOpen, Is.True);
            Assert.That(_inspector.IsShown, Is.False);
            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>Opening the inspector (tapping a building) closes the build menu's sheet.</summary>
        [Test]
        public void OpeningTheInspector_ClosesTheBuildSheet()
        {
            _menu.SetOpen(true);

            TapTile(2, 0);

            Assert.That(_inspector.IsShown, Is.True);
            Assert.That(_menu.IsOpen, Is.False);
        }

        /// <summary>Another building tapped while the inspector is shown, and the once-a-second refresh, are not openings: a sheet opened meanwhile is not closed by them.</summary>
        [Test]
        public void ARefreshOrAnotherBuilding_IsNotAnOpening()
        {
            int opened = 0;
            _inspector.Opened += () => opened++;
            TapTile(2, 0);
            Assert.That(opened, Is.EqualTo(1));

            _flow.Update(_display.Current!);
            TapTile(4, 0);

            Assert.That(_inspector.IsShown, Is.True);
            Assert.That(opened, Is.EqualTo(1));
        }

        /// <summary>Opening the sheet to pick another card while the placement flow holds a tile does not drop the tile: the tile is the placement's, not the inspector's.</summary>
        [Test]
        public void OpeningTheBuildSheet_WhilePlacing_KeepsThePickedTile()
        {
            _placement.SetType(new BuildingTypeId("house"));
            TapTile(3, 0);

            _menu.SetOpen(true);

            Assert.That(_world.Selection!.Selected, Is.EqualTo(new GridCoordinate(3, 0)));
        }

        /// <summary>A flow that was disposed no longer follows the ground.</summary>
        [Test]
        public void AfterDispose_TheFlowIgnoresTheGround()
        {
            _flow.Dispose();

            TapTile(2, 0);

            Assert.That(_inspector.IsShown, Is.False);
        }
    }
}
