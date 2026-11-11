#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Inspector;
using CityBuilder.Client.Presentation.Routes;
using CityBuilder.Simulation.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The courier route editor end to end in a running scene (CR-0008-T2 part C), with the real world, city store, command sender, inspector
    /// and editor sheet over a transport that answers when the test says: tap the courier station, see its courier line, press Edit routes, tap
    /// two buildings to add them as stops (the inspector stays closed and the lap's path is drawn), press Save, see one <c>set-route</c> request,
    /// and see the sheet close when the server answers. The test city (<see cref="RouteCity"/>) has two cottages (tiles 0 and 1), a sawmill on
    /// tile 2, a woodcutter on tile 4 and a courier station on tile 6 with one courier.
    /// </summary>
    public sealed class RouteEditorFlowTests
    {
        /// <summary>The courier station's building id in the test city.</summary>
        private const int StationId = 5;

        /// <summary>The object everything is made under.</summary>
        private GameObject _host = null!;

        /// <summary>The city's ground and tile selection.</summary>
        private CityWorld _world = null!;

        /// <summary>The network; answers when the test says.</summary>
        private ControllableTransport _transport = null!;

        /// <summary>The command sender the editor's Save uses.</summary>
        private CommandSender _sender = null!;

        /// <summary>The displayed city.</summary>
        private DisplayCity _display = null!;

        /// <summary>The inspector whose Edit routes button opens the editor.</summary>
        private BuildingInspector _inspector = null!;

        /// <summary>The inspector's flow.</summary>
        private InspectorFlow _inspectorFlow = null!;

        /// <summary>The editor's state.</summary>
        private RouteEditorModel _routes = null!;

        /// <summary>The editor's sheet.</summary>
        private RouteEditorScreen _screen = null!;

        /// <summary>The editor's flow.</summary>
        private RouteEditorFlow _flow = null!;

        /// <summary>Cancels commands still open at the end of a test.</summary>
        private CancellationTokenSource _cancel = null!;

        /// <summary>Before each test: a client with the route city, an 8 by 8 grid, the inspector and the editor; waits a frame for the trees.</summary>
        [UnitySetUp]
        public IEnumerator CreateFlow()
        {
            ContentResponseDto contentDto = new ApiJson().Deserialize<ContentResponseDto>(Fixture("content.json"))!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(contentDto));
            Assert.That(built.IsValid, Is.True);
            GameContent content = built.Content!;
            _host = new GameObject("Test route editor host");
            _world = _host.AddComponent<CityWorld>();
            var grid = new CityGrid(content.Rules.GridWidth, content.Rules.GridHeight);
            _world.ShowGrid(grid);
            var store = new ClientCityStore();
            var snapshotDto = new ApiJson().Deserialize<CitySnapshotDto>(RouteCity(10, withRoute: false))!;
            Assert.That(store.Apply(SnapshotMapper.ToSnapshot(snapshotDto).Value!), Is.True);
            _display = new DisplayCity(store, new DisplayClock(store, 1), content);
            _transport = new ControllableTransport();
            _sender = new CommandSender(new ApiClient("http://localhost:8080", 30, _transport, allowInsecureHttp: true), store, CommandPolicy.Default);
            _cancel = new CancellationTokenSource();
            _inspector = BuildingInspector.Create(_host.transform)!;
            _routes = new RouteEditorModel(content, grid);
            _routes.Update(_display.Current!);
            _screen = RouteEditorScreen.Create(_host.transform)!;
            _flow = new RouteEditorFlow(_routes, _world, _sender, _screen, _inspector, _cancel.Token);
            _inspectorFlow = new InspectorFlow(content, _world, _sender, _inspector, null, _cancel.Token, null, null, _routes);
            _display.Changed += () =>
            {
                _routes.Update(_display.Current!);
                _inspectorFlow.Update(_display.Current!);
            };
            _inspectorFlow.Update(_display.Current!);
            yield return null;
        }

        /// <summary>After each test: ends open commands and destroys everything.</summary>
        [TearDown]
        public void Destroy()
        {
            _flow.Dispose();
            _inspectorFlow.Dispose();
            _cancel.Cancel();
            _cancel.Dispose();
            Object.DestroyImmediate(_host);
        }

        /// <summary>The courier station's Re-energize button shows for a courier below full stamina and sends <c>reenergize-couriers</c>.</summary>
        [UnityTest]
        public IEnumerator Inspector_Reenergize_SendsTheCommand()
        {
            TapTile(6, 0);
            Button button = _inspector.Root!.Q<Button>(BuildingInspector.ReenergizeName);
            Assert.That(button.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            _inspector.PressReenergize();
            yield return null;

            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_transport.Requests[0].Url, Does.EndWith("/commands/reenergize-couriers"));
        }

        /// <summary>
        /// Tapping the station shows its courier line and Edit routes; pressing it opens the editor and hides the inspector; tapping two buildings
        /// adds them as stops and draws the lap; Save sends one <c>set-route</c>; the server's answer closes the sheet and clears the path.
        /// </summary>
        [UnityTest]
        public IEnumerator EditRoutes_AddTwoStops_SendsSetRoute_AndClosesOnSuccess()
        {
            TapTile(6, 0);
            Assert.That(_inspector.IsShown, Is.True);
            Assert.That(_inspector.Root!.Q<Label>(BuildingInspector.CouriersName).text, Does.StartWith("Courier 1 · At station"));
            Assert.That(_inspector.Root.Q<Button>(BuildingInspector.EditRoutesName).style.display.value, Is.EqualTo(DisplayStyle.Flex));

            _inspector.PressEditRoutes();

            Assert.That(_inspector.IsShown, Is.False, "the inspector leaves the map free for the taps");
            Assert.That(_routes.Phase, Is.EqualTo(RouteEditorPhase.Editing));
            Assert.That(_screen.IsOpen, Is.True);
            Assert.That(PanelOf().ClassListContains("route-panel--hidden"), Is.False);

            TapTile(2, 0);
            TapTile(4, 0);

            Assert.That(_inspector.IsShown, Is.False, "tapping a stop does not open its inspector");
            Assert.That(_routes.Lines.Count, Is.EqualTo(2));
            Assert.That(_screen.Root!.Query<VisualElement>(RouteEditorScreen.StopName).ToList().Count, Is.EqualTo(2));
            Assert.That(_world.Ground!.ShownPath, Is.Not.Empty);
            Assert.That(_routes.CanConfirm, Is.True, _routes.HintText);
            Assert.That(_screen.Root.Q<Button>(RouteEditorScreen.ConfirmName).enabledSelf, Is.True);

            _screen.PressConfirm();

            Assert.That(_sender.Pending.Count, Is.EqualTo(1));
            Assert.That(_sender.Pending[0].Intent.Kind, Is.EqualTo(CommandKind.SetRoute));
            Assert.That(_sender.Pending[0].Intent.Stops.Count, Is.EqualTo(2));
            yield return WaitUntil(() => _transport.OpenCount == 1);
            Assert.That(_transport.Requests[0].Url, Does.EndWith("/api/v1/commands/set-route"));

            _transport.Respond(200, RouteCity(11, withRoute: true));
            yield return WaitUntil(() => _sender.Pending.Count == 0);

            Assert.That(_routes.Phase, Is.EqualTo(RouteEditorPhase.Idle));
            Assert.That(_screen.IsOpen, Is.False);
            Assert.That(PanelOf().ClassListContains("route-panel--hidden"), Is.True);
            Assert.That(_world.Ground.ShownPath, Is.Empty);
        }

        /// <summary>Removing a stop and pressing the close button leave nothing sent; closing clears the path and hides the sheet.</summary>
        [Test]
        public void Close_AfterEdits_SendsNothingAndClearsThePath()
        {
            TapTile(6, 0);
            _inspector.PressEditRoutes();
            TapTile(2, 0);
            TapTile(4, 0);
            _screen.PressRemoveStop(1);

            Assert.That(_routes.Lines.Count, Is.EqualTo(1));
            Assert.That(_routes.CanConfirm, Is.False);

            _screen.PressClose();

            Assert.That(_routes.Phase, Is.EqualTo(RouteEditorPhase.Idle));
            Assert.That(_screen.IsOpen, Is.False);
            Assert.That(_sender.Pending.Count, Is.EqualTo(0));
            Assert.That(_world.Ground!.ShownPath, Is.Empty);
        }

        /// <summary>The Save button is off, with the reason shown, while the route has a single stop.</summary>
        [Test]
        public void OneStop_ShowsTheReasonAndKeepsSaveOff()
        {
            TapTile(6, 0);
            _inspector.PressEditRoutes();
            TapTile(2, 0);

            Assert.That(_screen.Root!.Q<Button>(RouteEditorScreen.ConfirmName).enabledSelf, Is.False);
            Assert.That(_screen.Root.Q<Label>(RouteEditorScreen.HintName).text, Is.EqualTo(RouteEditorModel.NeedsTwoStopsReason));
            _screen.PressConfirm();
            Assert.That(_sender.Pending.Count, Is.EqualTo(0), "a disabled Save sends nothing");
        }

        /// <summary>The sheet panel element.</summary>
        private VisualElement PanelOf()
        {
            return _screen.Root!.Q<VisualElement>(RouteEditorScreen.PanelName);
        }

        /// <summary>The player taps the tile (x, y) on the ground.</summary>
        private void TapTile(int x, int y)
        {
            _world.Selection!.Tap(new GridCoordinate(x, y));
        }

        /// <summary>The text of a file in <c>Tests/Fixtures</c>.</summary>
        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures", name));
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

        /// <summary>
        /// The test city as an answer's JSON: the real producing answer's clock, era and content with two cottages (tiles 0 and 1), a sawmill
        /// (tile 2), a woodcutter (tile 4) and the courier station <see cref="StationId"/> on tile 6 with one courier at the station; when
        /// <paramref name="withRoute"/> is set, slot 1 holds a route sawmill → woodcutter.
        /// </summary>
        private static string RouteCity(long stateVersion, bool withRoute)
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
                    3, "sawmill", 2, 0, 1, 3, 3, BuildingPhaseDto.Producing, 183, 243, 180, 180, 183, null,
                    new Dictionary<string, long> { ["logs"] = 10, ["planks"] = 10 }, new Dictionary<string, long> { ["logs"] = 40, ["planks"] = 20 }, none, null),
                new SnapshotBuildingDto(
                    4, "woodcutter", 4, 0, 1, 2, 2, BuildingPhaseDto.Producing, 183, 213, 60, 60, 183, null, none,
                    new Dictionary<string, long> { ["logs"] = 40 }, none, null),
                new SnapshotBuildingDto(StationId, "courier_station", 6, 0, 1, 1, 2, BuildingPhaseDto.Idle, 0, null, 0, 0, 0, "NoRecipe", none, none, none, null),
            };
            var couriers = new List<SnapshotCourierDto>
            {
                new SnapshotCourierDto(StationId, 1, CourierStateDto.AtStation, 0, null, null, 600, new List<SnapshotCourierLoadDto>()),
            };
            var routes = new List<SnapshotRouteDto>();
            if (withRoute)
            {
                routes.Add(new SnapshotRouteDto(StationId, 1, new List<SnapshotRouteStopDto>
                {
                    new SnapshotRouteStopDto(3, new List<SnapshotRouteActionDto> { new SnapshotRouteActionDto(RouteActionKindDto.Take, "logs", "all") }),
                    new SnapshotRouteStopDto(4, new List<SnapshotRouteActionDto> { new SnapshotRouteActionDto(RouteActionKindDto.Put, "logs", "all") }),
                }));
            }

            var cityDto = new SnapshotCityDto(
                city.Clock,
                city.Era,
                220,
                new Dictionary<string, long> { ["coins"] = 220, ["logs"] = 10, ["planks"] = 10 },
                new SnapshotWorkersDto(8, 6, 0, 2),
                new SnapshotBuildersDto(city.Builders.Slots, 0),
                new SnapshotResearchDto(null, null, null, none, new List<string>()),
                buildings,
                new List<SnapshotRoadDto>(),
                new List<SnapshotTripDto>(),
                couriers,
                routes);
            return json.Serialize(new CitySnapshotDto(real.RequestId, stateVersion, real.ServerTimeUtc, real.ContentVersion, cityDto, real.Settlement));
        }
    }
}
