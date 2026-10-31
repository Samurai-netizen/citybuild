#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Threading;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.City;
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
    /// The placement flow end to end in a running scene (M0-46), with the real world, menu state, bar and command sender over a transport that
    /// answers when the test says: pick a card, tap a tile, press Place, see the ghost while the request is open, and then either the real
    /// building from the new snapshot or the error; cancel at every step. Nothing is subtracted and no building is made before the server answers.
    /// </summary>
    public sealed class PlacementFlowTests
    {
        /// <summary>The cottage's id in the fixtures' content.</summary>
        private static readonly BuildingTypeId House = new BuildingTypeId("house");

        /// <summary>The sawmill's id.</summary>
        private static readonly BuildingTypeId Sawmill = new BuildingTypeId("sawmill");

        /// <summary>The object everything is made under.</summary>
        private GameObject _host = null!;

        /// <summary>The city's ground, ghosts and tile selection.</summary>
        private CityWorld _world = null!;

        /// <summary>The network; answers when the test says.</summary>
        private ControllableTransport _transport = null!;

        /// <summary>The city store the sender hands snapshots to.</summary>
        private ClientCityStore _store = null!;

        /// <summary>The displayed city, which the world's buildings follow.</summary>
        private DisplayCity _display = null!;

        /// <summary>The build menu's state.</summary>
        private BuildMenuModel _menu = null!;

        /// <summary>The flow under test.</summary>
        private PlacementFlow _flow = null!;

        /// <summary>The bar the flow listens to.</summary>
        private PlacementBar _bar = null!;

        /// <summary>How many times the flow asked for the menu's sheet to close.</summary>
        private int _menuClosed;

        /// <summary>Cancels commands still open at the end of a test.</summary>
        private CancellationTokenSource _cancel = null!;

        /// <summary>Before each test: a connected-looking client with the start city, an empty 8 by 8 grid and a flow over it; waits a frame for the bar's tree.</summary>
        [UnitySetUp]
        public IEnumerator CreateFlow()
        {
            yield return CreateFlowFor("snapshot-city-start.json");
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

        /// <summary>Makes the client for the city in the fixture <paramref name="snapshotFile"/>.</summary>
        private IEnumerator CreateFlowFor(string snapshotFile)
        {
            ContentResponseDto contentDto = new ApiJson().Deserialize<ContentResponseDto>(Fixture("content.json"))!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(contentDto));
            Assert.That(built.IsValid, Is.True);
            GameContent content = built.Content!;
            _menuClosed = 0;

            _host = new GameObject("Test placement host");
            _world = _host.AddComponent<CityWorld>();
            var grid = new CityGrid(content.Rules.GridWidth, content.Rules.GridHeight);
            _world.ShowGrid(grid);

            _store = new ClientCityStore();
            var snapshotDto = new ApiJson().Deserialize<CitySnapshotDto>(Fixture(snapshotFile))!;
            Assert.That(_store.Apply(SnapshotMapper.ToSnapshot(snapshotDto).Value!), Is.True);
            _display = new DisplayCity(_store, new DisplayClock(_store, 1), content);
            _display.Changed += () => _world.ShowCity(_display.Current!);
            _world.ShowCity(_display.Current!);

            _transport = new ControllableTransport();
            var sender = new CommandSender(new ApiClient("http://localhost:8080", 30, _transport), _store, CommandPolicy.Default);
            _menu = new BuildMenuModel(content, _display.Current!);
            var model = new PlacementFlowModel(content, grid);
            model.Update(_display.Current!);
            _cancel = new CancellationTokenSource();
            _bar = PlacementBar.Create(_host.transform, model)!;
            _flow = new PlacementFlow(model, _menu, () => _menuClosed++, _world, sender, _bar, () => _display.Current!.Era, _cancel.Token);
            yield return null;
        }

        /// <summary>The text of a file in <c>Tests/Fixtures</c>.</summary>
        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures", name));
        }

        /// <summary>The bar's label or button called <paramref name="name"/>.</summary>
        private T Element<T>(string name) where T : VisualElement
        {
            return _bar.Root!.Q<T>(name);
        }

        /// <summary>The player taps the tile (x, y) on the ground.</summary>
        private void TapTile(int x, int y)
        {
            _world.Selection!.Tap(new GridCoordinate(x, y));
        }

        /// <summary>Waits, a frame at a time, until the flow is in <paramref name="phase"/>; fails after a second of frames.</summary>
        private IEnumerator WaitForPhase(PlacementPhase phase)
        {
            for (int frame = 0; frame < 60 && _flow.Model.Phase != phase; frame++)
            {
                yield return null;
            }

            Assert.That(_flow.Model.Phase, Is.EqualTo(phase));
        }

        /// <summary>Picks the cottage, taps (0, 0), presses Place and waits for the request to be open; returns with the command on its way.</summary>
        private IEnumerator PlaceCottageAtOrigin()
        {
            _menu.TryToggle(House);
            TapTile(0, 0);
            _bar.PressPlace();
            yield return null;
            Assert.That(_transport.OpenCount, Is.EqualTo(1));
        }

        /// <summary>Nothing is shown until a card is picked: the bar is hidden and the tiles keep their ground colour.</summary>
        [Test]
        public void AtTheStart_NothingIsShown()
        {
            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.Idle));
            Assert.That(_bar.IsShown, Is.False);
            Assert.That(_world.Ground!.ShownHintAt(new GridCoordinate(3, 3)), Is.Null);
        }

        /// <summary>
        /// A picked card starts the flow: the sheet closes, the free tiles turn green and the bar says what to do; Place is not available until
        /// a tile is picked.
        /// </summary>
        [Test]
        public void PickingACard_ClosesTheSheet_HighlightsTheTiles_AndAsksForATile()
        {
            _menu.TryToggle(House);

            Assert.That(_menuClosed, Is.EqualTo(1));
            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.PickingTile));
            Assert.That(_world.Ground!.ShownHintAt(new GridCoordinate(3, 3)), Is.EqualTo(TileHint.Free));
            Assert.That(_bar.IsShown, Is.True);
            Assert.That(Element<Label>(PlacementBar.PromptName).text, Is.EqualTo("Cottage: tap a tile to build on"));
            Assert.That(Element<Button>(PlacementBar.PlaceName).enabledSelf, Is.False);
            Assert.That(Element<Button>(PlacementBar.CancelName).enabledSelf, Is.True);
        }

        /// <summary>Switching to another card keeps the flow going and does not close the sheet again.</summary>
        [Test]
        public void SwitchingTheCard_KeepsTheFlowAndTheTile()
        {
            _menu.TryToggle(House);
            TapTile(2, 2);

            _menu.TryToggle(Sawmill);

            Assert.That(_flow.Model.TypeId, Is.EqualTo(Sawmill));
            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.Confirming));
            Assert.That(_menuClosed, Is.EqualTo(1));
        }

        /// <summary>A tile tapped before any card is picked is not part of the flow: picking a card then asks for a tile.</summary>
        [Test]
        public void ATileTappedBeforeACard_IsForgottenWhenTheCardIsPicked()
        {
            TapTile(4, 4);
            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.Idle));

            _menu.TryToggle(House);

            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.PickingTile));
            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>A tapped tile asks for confirmation; tapping it again goes back to picking a tile.</summary>
        [Test]
        public void TappingATile_AsksToConfirm_AndTappingItAgainGoesBack()
        {
            _menu.TryToggle(House);

            TapTile(2, 3);
            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.Confirming));
            Assert.That(Element<Label>(PlacementBar.PromptName).text, Is.EqualTo("Build Cottage at (2, 3)?"));
            Assert.That(Element<Button>(PlacementBar.PlaceName).enabledSelf, Is.True);

            TapTile(2, 3);
            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.PickingTile));
            Assert.That(Element<Button>(PlacementBar.PlaceName).enabledSelf, Is.False);
        }

        /// <summary>Occupied tiles of the last snapshot are red and free ones green; an occupied tile warns but can still be confirmed.</summary>
        [UnityTest]
        public IEnumerator OccupiedTiles_AreRed_AndWarnButCanBeConfirmed()
        {
            _flow.Dispose();
            _cancel.Cancel();
            Object.DestroyImmediate(_host);
            yield return CreateFlowFor("snapshot-city-settled-buildings.json");

            _menu.TryToggle(Sawmill);
            TapTile(1, 0);

            Assert.That(_world.Ground!.ShownHintAt(new GridCoordinate(0, 0)), Is.EqualTo(TileHint.Occupied));
            Assert.That(_world.Ground.ShownHintAt(new GridCoordinate(1, 0)), Is.EqualTo(TileHint.Occupied));
            Assert.That(_world.Ground.ShownHintAt(new GridCoordinate(2, 0)), Is.EqualTo(TileHint.Free));
            Assert.That(Element<Label>(PlacementBar.HintName).text, Is.EqualTo("This tile is occupied"));
            Assert.That(Element<Button>(PlacementBar.PlaceName).enabledSelf, Is.True);
        }

        /// <summary>
        /// Place gives the server the command and a translucent ghost stands on the tile while it is open; until the server answers nothing
        /// is subtracted and no building exists.
        /// </summary>
        [UnityTest]
        public IEnumerator PressingPlace_SendsTheCommand_ShowsAGhost_AndChangesNothingElse()
        {
            long coinsBefore = _display.Current!.Inventory[new ItemId("coins")];

            yield return PlaceCottageAtOrigin();

            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.Sending));
            Assert.That(_transport.Requests[0].Url, Does.EndWith("/commands/place-building"));
            Assert.That(_transport.Requests[0].JsonBody, Does.Contain("\"buildingTypeId\":\"house\"").And.Contain("\"x\":0").And.Contain("\"y\":0"));
            Assert.That(_world.Ghosts!.Count, Is.EqualTo(1));
            Assert.That(_world.Buildings!.Count, Is.EqualTo(0), "no building before the server says so");
            Assert.That(_display.Current.Inventory[new ItemId("coins")], Is.EqualTo(coinsBefore), "nothing is subtracted before the server says so");
            Assert.That(Element<Label>(PlacementBar.PromptName).text, Is.EqualTo("Building Cottage at (0, 0)…"));
            Assert.That(Element<Button>(PlacementBar.PlaceName).enabledSelf, Is.False);
            Assert.That(Element<Button>(PlacementBar.CancelName).enabledSelf, Is.False, "a command on its way cannot be taken back");
        }

        /// <summary>A second press on Place while the command is open sends nothing more.</summary>
        [UnityTest]
        public IEnumerator PressingPlaceTwice_SendsOnce()
        {
            yield return PlaceCottageAtOrigin();

            _bar.PressPlace();
            yield return null;

            Assert.That(_transport.Requests.Count, Is.EqualTo(1));
        }

        /// <summary>The server accepted: the real building comes from the new snapshot, the ghost is gone and the flow is over.</summary>
        [UnityTest]
        public IEnumerator WhenTheServerAccepts_TheBuildingComesFromTheSnapshot_AndTheGhostGoes()
        {
            yield return PlaceCottageAtOrigin();

            _transport.Respond(200, Fixture("snapshot-command-place-building.json"));
            yield return WaitForPhase(PlacementPhase.Idle);

            Assert.That(_world.Buildings!.Count, Is.EqualTo(1));
            Assert.That(_world.Buildings.TryGetView(1, out BuildingView? view), Is.True);
            Assert.That(view!.TypeId, Is.EqualTo("house"));
            Assert.That(_world.Ghosts!.Count, Is.EqualTo(0));
            Assert.That(_display.Current!.Inventory[new ItemId("coins")], Is.EqualTo(500), "the coins are the server's");
            Assert.That(_menu.Selected, Is.Null);
            Assert.That(_world.Selection!.Selected, Is.Null);
            Assert.That(_world.Ground!.ShownHintAt(new GridCoordinate(3, 3)), Is.Null, "the highlights are gone");
            yield return null;
            Assert.That(_bar.IsShown, Is.False);
        }

        /// <summary>The server refused: the ghost is gone, no building exists, the error is shown, and the card stays picked to try another tile.</summary>
        [UnityTest]
        public IEnumerator WhenTheServerRefuses_TheGhostGoes_AndTheErrorIsShown()
        {
            yield return PlaceCottageAtOrigin();

            _transport.Respond(409, Fixture("error-409-TILE_OCCUPIED.json"));
            yield return WaitForPhase(PlacementPhase.PickingTile);
            yield return null;

            Assert.That(_world.Ghosts!.Count, Is.EqualTo(0));
            Assert.That(_world.Buildings!.Count, Is.EqualTo(0));
            Assert.That(_display.Current!.Inventory[new ItemId("coins")], Is.EqualTo(700));
            Assert.That(_flow.Model.ErrorText, Does.StartWith("TILE_OCCUPIED"));
            Assert.That(Element<Label>(PlacementBar.ErrorName).text, Does.StartWith("TILE_OCCUPIED"));
            Assert.That(_menu.Selected, Is.EqualTo(House));
            Assert.That(_world.Selection!.Selected, Is.Null);
            Assert.That(_bar.IsShown, Is.True);
        }

        /// <summary>After a refusal the same card can be placed on another tile, with a new request.</summary>
        [UnityTest]
        public IEnumerator AfterARefusal_AnotherTileCanBeTried()
        {
            yield return PlaceCottageAtOrigin();
            _transport.Respond(409, Fixture("error-409-TILE_OCCUPIED.json"));
            yield return WaitForPhase(PlacementPhase.PickingTile);

            TapTile(5, 5);
            _bar.PressPlace();
            yield return null;

            Assert.That(_transport.Requests.Count, Is.EqualTo(2));
            Assert.That(_transport.Requests[1].JsonBody, Does.Contain("\"x\":5").And.Contain("\"y\":5"));
            Assert.That(_flow.Model.ErrorText, Is.Empty);
        }

        /// <summary>A lost connection is a failure too: the ghost goes after the sender's retries and the error is shown.</summary>
        [UnityTest]
        public IEnumerator WhenTheConnectionIsLost_TheFlowRecovers()
        {
            yield return PlaceCottageAtOrigin();

            // The sender repeats a lost request up to three times, waiting half a second and then one second (real time) between.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float deadline = Time.realtimeSinceStartup + 5f;
                while (_transport.OpenCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(_transport.OpenCount, Is.EqualTo(1), "attempt " + (attempt + 1));
                _transport.FailWithNetworkError();
                yield return null;
            }

            yield return WaitForPhase(PlacementPhase.PickingTile);
            Assert.That(_flow.Model.ErrorText, Does.StartWith("CLIENT_NETWORK_ERROR"));
            Assert.That(_world.Ghosts!.Count, Is.EqualTo(0));
        }

        /// <summary>Changing the card in the menu while a command is open changes nothing of that command; if it is refused the menu shows the card again.</summary>
        [UnityTest]
        public IEnumerator ChangingTheMenuWhileSending_IsIgnored_AndUndoneByARefusal()
        {
            yield return PlaceCottageAtOrigin();

            _menu.TryToggle(House);
            TapTile(6, 6);
            Assert.That(_flow.Model.TypeId, Is.EqualTo(House));
            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.Sending));

            _transport.Respond(409, Fixture("error-409-TILE_OCCUPIED.json"));
            yield return WaitForPhase(PlacementPhase.PickingTile);

            Assert.That(_menu.Selected, Is.EqualTo(House), "the menu shows the card that is still picked");
            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>Cancel with a card picked and no tile ends the flow: the card is dropped, the tiles lose their colour, the bar goes.</summary>
        [UnityTest]
        public IEnumerator Cancel_WhilePickingATile_EndsTheFlow()
        {
            _menu.TryToggle(House);

            _bar.PressCancel();
            yield return null;

            AssertFlowIsOver();
        }

        /// <summary>Cancel with a tile picked ends the flow and sends nothing.</summary>
        [UnityTest]
        public IEnumerator Cancel_WhileConfirming_EndsTheFlow_AndSendsNothing()
        {
            _menu.TryToggle(House);
            TapTile(3, 3);

            _bar.PressCancel();
            yield return null;

            AssertFlowIsOver();
            Assert.That(_transport.Requests.Count, Is.EqualTo(0));
        }

        /// <summary>Cancel does nothing while the command is on its way: its result still comes.</summary>
        [UnityTest]
        public IEnumerator Cancel_WhileSending_DoesNothing()
        {
            yield return PlaceCottageAtOrigin();

            _bar.PressCancel();
            yield return null;

            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.Sending));
            Assert.That(_world.Ghosts!.Count, Is.EqualTo(1));
        }

        /// <summary>Tapping the picked card again in the menu cancels the flow too.</summary>
        [UnityTest]
        public IEnumerator DeselectingTheCard_EndsTheFlow()
        {
            _menu.TryToggle(House);
            TapTile(3, 3);

            _menu.TryToggle(House);
            yield return null;

            AssertFlowIsOver();
        }

        /// <summary>The card, the tile, the highlights and the bar are all gone.</summary>
        private void AssertFlowIsOver()
        {
            Assert.That(_flow.Model.Phase, Is.EqualTo(PlacementPhase.Idle));
            Assert.That(_menu.Selected, Is.Null);
            Assert.That(_world.Selection!.Selected, Is.Null);
            Assert.That(_world.Ground!.ShownHintAt(new GridCoordinate(3, 3)), Is.Null);
            Assert.That(_bar.IsShown, Is.False);
        }
    }
}
