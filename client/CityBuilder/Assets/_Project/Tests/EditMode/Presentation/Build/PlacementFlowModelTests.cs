#nullable enable

using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Build
{
    /// <summary>
    /// The placement flow without a screen (M0-46): the phases from a picked card to a confirmed command, the free and occupied hints from a
    /// city, the words of the bar, and what a success, a failure and a cancel each leave behind.
    /// </summary>
    public sealed class PlacementFlowModelTests
    {
        /// <summary>The sawmill's id in the fixtures' content.</summary>
        private static readonly BuildingTypeId Sawmill = new BuildingTypeId("sawmill");

        /// <summary>The cottage's id.</summary>
        private static readonly BuildingTypeId House = new BuildingTypeId("house");

        /// <summary>The flow under test, over the 8 by 8 grid of the fixtures' content.</summary>
        private PlacementFlowModel _flow = null!;

        /// <summary>How many times <see cref="PlacementFlowModel.Changed"/> was raised.</summary>
        private int _changed;

        /// <summary>How many times <see cref="PlacementFlowModel.Ended"/> was raised.</summary>
        private int _ended;

        /// <summary>Before each test: a flow with nothing picked, over a city with no buildings.</summary>
        [SetUp]
        public void CreateFlow()
        {
            _flow = new PlacementFlowModel(BuildMenuTestData.Content(), new CityGrid(8, 8));
            _flow.Update(BuildMenuTestData.StartCity());
            _changed = 0;
            _ended = 0;
            _flow.Changed += () => _changed++;
            _flow.Ended += () => _ended++;
        }

        /// <summary>A refusal as the server sends it.</summary>
        private static ApiError Refusal(string code)
        {
            return new ApiError(code, 409, "Something the server said.", null, null, null);
        }

        /// <summary>Picks the sawmill and the tile (3, 4) and confirms; returns the intent that was made.</summary>
        private CommandIntent ConfirmSawmillAt34()
        {
            _flow.SetType(Sawmill);
            _flow.SetTile(new GridCoordinate(3, 4));
            Assert.That(_flow.TryConfirm(out CommandIntent? intent), Is.True);
            return intent!;
        }

        /// <summary>Nothing is picked at the start: the flow is idle, shows no hints and has nothing to cancel.</summary>
        [Test]
        public void AtTheStart_TheFlowIsIdle()
        {
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Idle));
            Assert.That(_flow.ShowsHints, Is.False);
            Assert.That(_flow.CanConfirm, Is.False);
            Assert.That(_flow.CanCancel, Is.False);
            Assert.That(_flow.PromptText, Is.Empty);
        }

        /// <summary>A card moves the flow to picking a tile and the bar names the building; a tile then moves it to confirming.</summary>
        [Test]
        public void PickingACardAndATile_MovesThroughThePhases()
        {
            _flow.SetType(Sawmill);
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.PickingTile));
            Assert.That(_flow.ShowsHints, Is.True);
            Assert.That(_flow.CanCancel, Is.True);
            Assert.That(_flow.PromptText, Is.EqualTo("Sawmill: tap a tile to build on"));

            _flow.SetTile(new GridCoordinate(3, 4));
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Confirming));
            Assert.That(_flow.CanConfirm, Is.True);
            Assert.That(_flow.PromptText, Is.EqualTo("Build Sawmill at (3, 4)?"));
        }

        /// <summary>A tile picked with no card does not start the flow (the ground's selection is also used for other things).</summary>
        [Test]
        public void ATileWithNoCard_StaysIdle()
        {
            _flow.SetTile(new GridCoordinate(1, 1));

            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Idle));
            Assert.That(_flow.CanConfirm, Is.False);
        }

        /// <summary>Dropping the card drops the tile with it; dropping only the tile goes back to picking one.</summary>
        [Test]
        public void ClearingTheCard_ClearsTheTile_AndClearingTheTileGoesBackToPicking()
        {
            _flow.SetType(Sawmill);
            _flow.SetTile(new GridCoordinate(3, 4));

            _flow.SetTile(null);
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.PickingTile));

            _flow.SetTile(new GridCoordinate(3, 4));
            _flow.SetType(null);
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Idle));
            Assert.That(_flow.Tile, Is.Null);
        }

        /// <summary>Setting what is already set raises nothing.</summary>
        [Test]
        public void SettingTheSameTypeOrTile_RaisesNothing()
        {
            _flow.SetType(Sawmill);
            _flow.SetTile(new GridCoordinate(3, 4));
            _changed = 0;

            _flow.SetType(Sawmill);
            _flow.SetTile(new GridCoordinate(3, 4));

            Assert.That(_changed, Is.EqualTo(0));
        }

        /// <summary>A tile with a building of any phase is occupied (red), every other tile of the grid is free (green).</summary>
        [Test]
        public void Update_MarksTheTilesOfBuildingsOccupied_AndTheRestFree()
        {
            // BuildMenuTestData puts building n on tile (n, 0): two buildings under construction are on (1, 0) and (2, 0).
            _flow.Update(BuildMenuTestData.City(700, 40, 10, new string[0], 2));

            Assert.That(_flow.HintAt(new GridCoordinate(1, 0)), Is.EqualTo(TileHint.Occupied));
            Assert.That(_flow.HintAt(new GridCoordinate(2, 0)), Is.EqualTo(TileHint.Occupied));
            Assert.That(_flow.HintAt(new GridCoordinate(0, 0)), Is.EqualTo(TileHint.Free));
            Assert.That(_flow.HintAt(new GridCoordinate(7, 7)), Is.EqualTo(TileHint.Free));
            int occupied = 0;
            foreach (TileHint hint in _flow.Hints)
            {
                occupied += hint == TileHint.Occupied ? 1 : 0;
            }

            Assert.That(_flow.Hints.Length, Is.EqualTo(64));
            Assert.That(occupied, Is.EqualTo(2));
        }

        /// <summary>A tile outside the grid is not a hint (and not an error); a building the grid cannot hold is ignored.</summary>
        [Test]
        public void HintAt_ATileOutsideTheGrid_IsFree_AndABuildingOutsideTheGridIsIgnored()
        {
            // Ten buildings on (1..10, 0): the last three lie past the 8 columns.
            Assert.DoesNotThrow(() => _flow.Update(BuildMenuTestData.City(700, 40, 10, new string[0], 10)));

            Assert.That(_flow.HintAt(new GridCoordinate(8, 0)), Is.EqualTo(TileHint.Free));
            Assert.That(_flow.HintAt(new GridCoordinate(-1, 3)), Is.EqualTo(TileHint.Free));
            Assert.That(_flow.HintAt(new GridCoordinate(7, 0)), Is.EqualTo(TileHint.Occupied));
        }

        /// <summary>The hints change when a snapshot adds or removes a building and raise <c>Changed</c> then; the same city raises nothing.</summary>
        [Test]
        public void Update_RaisesChangedOnlyWhenAHintDiffers()
        {
            _flow.Update(BuildMenuTestData.City(700, 40, 10, new string[0], 1));
            Assert.That(_changed, Is.EqualTo(1));

            _flow.Update(BuildMenuTestData.City(700, 40, 10, new string[0], 1));
            Assert.That(_changed, Is.EqualTo(1), "the same buildings, so the same hints");

            _flow.Update(BuildMenuTestData.StartCity());
            Assert.That(_changed, Is.EqualTo(2));
            Assert.That(_flow.HintAt(new GridCoordinate(1, 0)), Is.EqualTo(TileHint.Free));
        }

        /// <summary>An occupied tile can be picked and warns, but cannot be confirmed: no intent is made for it.</summary>
        [Test]
        public void AnOccupiedTile_Warns_AndCannotBeConfirmed()
        {
            _flow.Update(BuildMenuTestData.City(700, 40, 10, new string[0], 1));
            _flow.SetType(House);
            _flow.SetTile(new GridCoordinate(1, 0));

            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Confirming));
            Assert.That(_flow.HintText, Is.EqualTo("This tile is occupied"));
            Assert.That(_flow.CanConfirm, Is.False);
            Assert.That(_flow.TryConfirm(out CommandIntent? intent), Is.False);
            Assert.That(intent, Is.Null);
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Confirming), "nothing was started");
            Assert.That(_flow.CanCancel, Is.True);

            _flow.SetTile(new GridCoordinate(5, 5));
            Assert.That(_flow.HintText, Is.Empty);
            Assert.That(_flow.CanConfirm, Is.True);
        }

        /// <summary>A snapshot that occupies the picked tile while the player is deciding takes the confirm away; one that frees it gives it back.</summary>
        [Test]
        public void WhenASnapshotOccupiesThePickedTile_ConfirmingStops()
        {
            _flow.SetType(House);
            _flow.SetTile(new GridCoordinate(1, 0));
            Assert.That(_flow.CanConfirm, Is.True);
            _changed = 0;

            _flow.Update(BuildMenuTestData.City(700, 40, 10, new string[0], 1));

            Assert.That(_flow.CanConfirm, Is.False);
            Assert.That(_changed, Is.GreaterThan(0), "the bar must hear about it");

            _flow.Update(BuildMenuTestData.StartCity());
            Assert.That(_flow.CanConfirm, Is.True);
        }

        /// <summary>Confirming makes the place-building intent for the picked type and tile, and the flow is then sending.</summary>
        [Test]
        public void TryConfirm_MakesThePlaceBuildingIntent_AndStartsSending()
        {
            CommandIntent intent = ConfirmSawmillAt34();

            Assert.That(intent, Is.EqualTo(CommandIntent.PlaceBuilding("sawmill", 3, 4)));
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Sending));
            Assert.That(_flow.PromptText, Is.EqualTo("Building Sawmill at (3, 4)…"));
        }

        /// <summary>Without a card, or without a tile, there is nothing to confirm.</summary>
        [Test]
        public void TryConfirm_WithoutACardAndATile_ReturnsFalse()
        {
            Assert.That(_flow.TryConfirm(out CommandIntent? none), Is.False);
            Assert.That(none, Is.Null);

            _flow.SetType(Sawmill);
            Assert.That(_flow.TryConfirm(out CommandIntent? noTile), Is.False);
            Assert.That(noTile, Is.Null);
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.PickingTile));
        }

        /// <summary>A second press on Place while the command is on its way makes no second intent.</summary>
        [Test]
        public void TryConfirm_WhileSending_ReturnsFalse()
        {
            ConfirmSawmillAt34();

            Assert.That(_flow.CanConfirm, Is.False);
            Assert.That(_flow.TryConfirm(out CommandIntent? again), Is.False);
            Assert.That(again, Is.Null);
        }

        /// <summary>A command that was sent cannot be cancelled: the server may already have applied it.</summary>
        [Test]
        public void Cancel_WhileSending_DoesNothing()
        {
            ConfirmSawmillAt34();
            _changed = 0;

            Assert.That(_flow.CanCancel, Is.False);
            _flow.Cancel();

            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Sending));
            Assert.That(_changed, Is.EqualTo(0));
            Assert.That(_ended, Is.EqualTo(0));
        }

        /// <summary>What was confirmed stays what it was while the command is on its way: another card or tile changes nothing.</summary>
        [Test]
        public void SetTypeAndSetTile_WhileSending_ChangeNothing()
        {
            ConfirmSawmillAt34();
            _changed = 0;

            _flow.SetType(null);
            _flow.SetType(House);
            _flow.SetTile(new GridCoordinate(0, 0));
            _flow.SetTile(null);

            Assert.That(_flow.TypeId, Is.EqualTo(Sawmill));
            Assert.That(_flow.Tile, Is.EqualTo(new GridCoordinate(3, 4)));
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Sending));
            Assert.That(_changed, Is.EqualTo(0));
        }

        /// <summary>The server accepted: the flow ends, the card and the tile are dropped, and the holders are told once.</summary>
        [Test]
        public void Succeeded_EndsTheFlow()
        {
            ConfirmSawmillAt34();

            _flow.Succeeded();

            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Idle));
            Assert.That(_flow.TypeId, Is.Null);
            Assert.That(_flow.Tile, Is.Null);
            Assert.That(_flow.ErrorText, Is.Empty);
            Assert.That(_ended, Is.EqualTo(1));
        }

        /// <summary>
        /// A refusal keeps the card so another tile can be tried, drops the tile, keeps the error to show, and ends nothing; picking a
        /// tile again clears the error.
        /// </summary>
        [Test]
        public void Failed_KeepsTheCard_DropsTheTile_AndKeepsTheError()
        {
            ConfirmSawmillAt34();

            _flow.Failed(Refusal("TILE_OCCUPIED"));

            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.PickingTile));
            Assert.That(_flow.TypeId, Is.EqualTo(Sawmill));
            Assert.That(_flow.Tile, Is.Null);
            Assert.That(_flow.ErrorText, Is.EqualTo("TILE_OCCUPIED: Something the server said."));
            Assert.That(_ended, Is.EqualTo(0));

            _flow.SetTile(new GridCoordinate(0, 0));
            Assert.That(_flow.ErrorText, Is.Empty);
        }

        /// <summary>After a refusal the same card can be placed again: a new intent is made.</summary>
        [Test]
        public void AfterAFailure_TheCardCanBePlacedOnAnotherTile()
        {
            ConfirmSawmillAt34();
            _flow.Failed(Refusal("NO_FREE_BUILDER"));

            _flow.SetTile(new GridCoordinate(5, 5));

            Assert.That(_flow.TryConfirm(out CommandIntent? intent), Is.True);
            Assert.That(intent, Is.EqualTo(CommandIntent.PlaceBuilding("sawmill", 5, 5)));
        }

        /// <summary>An ending that arrives when nothing was sent changes nothing.</summary>
        [Test]
        public void SucceededOrFailed_WithNothingSent_ChangeNothing()
        {
            _flow.SetType(Sawmill);
            _changed = 0;

            _flow.Succeeded();
            _flow.Failed(Refusal("TILE_OCCUPIED"));

            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.PickingTile));
            Assert.That(_changed, Is.EqualTo(0));
            Assert.That(_ended, Is.EqualTo(0));
        }

        /// <summary>A failure with no error is a programming mistake, not a state.</summary>
        [Test]
        public void Failed_WithoutAnError_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => _flow.Failed(null!));
        }

        /// <summary>Cancel ends the flow while picking a tile, and while confirming; both tell the holders.</summary>
        [Test]
        public void Cancel_WhilePickingAndWhileConfirming_EndsTheFlow()
        {
            _flow.SetType(Sawmill);
            _flow.Cancel();
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Idle));
            Assert.That(_ended, Is.EqualTo(1));

            _flow.SetType(Sawmill);
            _flow.SetTile(new GridCoordinate(2, 2));
            _flow.Cancel();
            Assert.That(_flow.Phase, Is.EqualTo(PlacementPhase.Idle));
            Assert.That(_flow.Tile, Is.Null);
            Assert.That(_ended, Is.EqualTo(2));
        }

        /// <summary>Cancel with nothing picked does nothing and raises nothing.</summary>
        [Test]
        public void Cancel_WhenIdle_DoesNothing()
        {
            _flow.Cancel();

            Assert.That(_changed, Is.EqualTo(0));
            Assert.That(_ended, Is.EqualTo(0));
        }

        /// <summary>A card that is not in the content is named by its id rather than crashing the bar.</summary>
        [Test]
        public void ATypeTheContentDoesNotHave_IsNamedByItsId()
        {
            _flow.SetType(new BuildingTypeId("castle"));

            Assert.That(_flow.PromptText, Is.EqualTo("castle: tap a tile to build on"));
        }
    }
}
