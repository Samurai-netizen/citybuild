#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Routes;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Routes
{
    /// <summary>
    /// The courier route editor's state (CR-0008-T2 part C): opening a slot, adding stops by tapping buildings, the rules that refuse a repeat
    /// neighbour or too many stops, the actions of a stop (kind, item, amount), the lap time and path, the Save button's reasons and the
    /// <c>set-route</c> intent it sends.
    /// </summary>
    public sealed class RouteEditorModelTests
    {
        /// <summary>The courier station.</summary>
        private const int Station = 1;

        /// <summary>A sawmill holding logs.</summary>
        private const int Sawmill = 2;

        /// <summary>A cottage.</summary>
        private const int Cottage = 3;

        /// <summary>A second cottage.</summary>
        private const int Cottage2 = 4;

        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>The editor under test.</summary>
        private RouteEditorModel _editor = null!;

        /// <summary>Before each test: the content and an editor over an 8 by 8 grid.</summary>
        [SetUp]
        public void Prepare()
        {
            _content = HudTestData.Content();
            _editor = new RouteEditorModel(_content, new CityGrid(8, 8));
        }

        /// <summary>Opening slot 1 of a station with a courier works; a slot with no courier and a building that is no station do not.</summary>
        [Test]
        public void Open_OnlyAStationSlotWithACourier_Succeeds()
        {
            _editor.Update(City(couriers: 1));

            Assert.That(_editor.Open(Station, 2), Is.False, "slot 2 has no courier");
            Assert.That(_editor.Open(Sawmill, 1), Is.False, "a sawmill is no station");
            Assert.That(_editor.Open(99, 1), Is.False, "no such building");
            Assert.That(_editor.Open(Station, 1), Is.True);
            Assert.That(_editor.Phase, Is.EqualTo(RouteEditorPhase.Editing));
            Assert.That(_editor.TitleText, Does.StartWith("Courier 1 of "));
        }

        /// <summary>Tapping a building adds it as a stop that takes what it stores; empty ground changes nothing.</summary>
        [Test]
        public void SelectTile_OnABuilding_AddsAStopThatTakesFromIt()
        {
            OpenEmpty();

            _editor.SelectTile(new GridCoordinate(7, 7));
            Assert.That(_editor.Lines, Is.Empty, "empty ground adds nothing");

            _editor.SelectTile(new GridCoordinate(4, 0));
            Assert.That(_editor.Lines, Has.Count.EqualTo(1));
            Assert.That(_editor.Lines[0].Title, Does.StartWith("1. "));
            Assert.That(_editor.Lines[0].Actions[0], Does.StartWith("Take "));
        }

        /// <summary>The same building twice in a row, and the first building again as the last stop, are refused with a note.</summary>
        [Test]
        public void AddStop_RepeatingANeighbour_IsRefusedWithANote()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);

            Assert.That(_editor.AddStop(Sawmill), Is.False);
            Assert.That(_editor.NoteText, Is.Not.Empty);
            Assert.That(_editor.AddStop(Cottage), Is.True);
            Assert.That(_editor.NoteText, Is.Empty, "an accepted edit clears the note");
            Assert.That(_editor.Lines, Has.Count.EqualTo(2));
        }

        /// <summary>The first building may come again later in the route, but the lap cannot close on it: Save names that.</summary>
        [Test]
        public void Save_WhenTheLastStopIsTheFirstAgain_NamesTheReason()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);
            _editor.AddStop(Cottage);
            _editor.AddStop(Sawmill);

            Assert.That(_editor.Lines, Has.Count.EqualTo(3), "adding it is allowed");
            Assert.That(_editor.CanConfirm, Is.False);
            Assert.That(_editor.HintText, Is.EqualTo(RouteEditorModel.LastIsFirstReason));

            _editor.AddStop(Station);

            Assert.That(_editor.CanConfirm, Is.True, _editor.HintText);
        }

        /// <summary>A route is full at the content's limit of stops.</summary>
        [Test]
        public void AddStop_AtTheStopLimit_IsRefused()
        {
            OpenEmpty();
            int limit = _content.Rules.MaxRouteStops;
            for (int i = 0; i < limit; i++)
            {
                // Rotate four buildings so no two neighbours match (the last and the first included): when the rotation would end on the
                // first building again, the last stop is the cottage instead.
                int building = new[] { Sawmill, Cottage, Station, Cottage2 }[i % 4];
                if (i == limit - 1 && building == Sawmill && i > 0)
                {
                    building = Cottage;
                }

                Assert.That(_editor.AddStop(building), Is.True, "stop " + (i + 1));
            }

            Assert.That(_editor.CanAddStop, Is.False);
            Assert.That(_editor.AddStop(Cottage), Is.False);
            Assert.That(_editor.NoteText, Does.Contain("at most"));
            Assert.That(_editor.Lines, Has.Count.EqualTo(limit));
        }

        /// <summary>Two stops give a lap time, a path and a working Save; one stop names the two-stops reason.</summary>
        [Test]
        public void Stops_TwoGiveALapAndEnableSave_OneDoesNot()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);

            Assert.That(_editor.CanConfirm, Is.False);
            Assert.That(_editor.HintText, Is.EqualTo(RouteEditorModel.NeedsTwoStopsReason));

            _editor.AddStop(Cottage);

            Assert.That(_editor.CanConfirm, Is.True, _editor.HintText);
            Assert.That(_editor.LapSeconds, Is.GreaterThan(0));
            Assert.That(_editor.Path, Is.Not.Empty);
            Assert.That(_editor.SummaryText, Does.StartWith("2 stops · lap "));
            Assert.That(_editor.Lines[0].WalkText, Does.StartWith("Next stop in "));
            Assert.That(_editor.Lines[1].WalkText, Does.StartWith("Next stop in "), "the lap closes from the last stop to the first");
        }

        /// <summary>Without a built Scriptorium Save is off and says to build one (CR-0008 D15); clearing needs none.</summary>
        [Test]
        public void Save_WithoutAScriptorium_NamesTheRouteBuilding()
        {
            _editor.Update(City(couriers: 1, scriptorium: false));
            _editor.Open(Station, 1);
            _editor.AddStop(Sawmill);
            _editor.AddStop(Cottage);

            Assert.That(_editor.CanConfirm, Is.False);
            Assert.That(_editor.HintText, Does.StartWith("Build a Scriptorium first"));
        }

        /// <summary>A courier with deliveries takes no route: Save says to cancel them first (CR-0008 D19), and the editor opens on a courier without any.</summary>
        [Test]
        public void Save_CourierWithDeliveries_IsBusy_AndTheEditorOpensOnAFreeOne()
        {
            _editor.Update(City(couriers: 2, lines: new[] { new DeliveryLine(1, Station, 1, Sawmill, Cottage, new ItemId("logs"), 5) }));

            Assert.That(_editor.FirstFreeSlot(Station), Is.EqualTo(2));
            _editor.Open(Station, 1);
            _editor.AddStop(Sawmill);
            _editor.AddStop(Cottage);

            Assert.That(_editor.CanConfirm, Is.False);
            Assert.That(_editor.HintText, Is.EqualTo(RouteEditorModel.CourierBusyReason));
        }

        /// <summary>A route equal to the saved one has nothing to save.</summary>
        [Test]
        public void Open_WithASavedRoute_ShowsItAndOffersNoSaveUntilChanged()
        {
            _editor.Update(City(couriers: 1, saved: SavedRoute()));
            _editor.Open(Station, 1);

            Assert.That(_editor.Lines, Has.Count.EqualTo(2));
            Assert.That(_editor.CanConfirm, Is.False);
            Assert.That(_editor.HintText, Is.EqualTo(RouteEditorModel.NoChangesReason));

            _editor.ToggleKind(0, 0);

            Assert.That(_editor.CanConfirm, Is.True, _editor.HintText);

            _editor.Revert();

            Assert.That(_editor.CanConfirm, Is.False);
        }

        /// <summary>Removing every stop of a saved route turns Save into Clear route, and it works.</summary>
        [Test]
        public void RemoveStop_AllOfASavedRoute_OffersClearRoute()
        {
            _editor.Update(City(couriers: 1, saved: SavedRoute()));
            _editor.Open(Station, 1);

            _editor.RemoveStop(0);
            _editor.RemoveStop(0);

            Assert.That(_editor.Lines, Is.Empty);
            Assert.That(_editor.ConfirmText, Is.EqualTo("Clear route"));
            Assert.That(_editor.CanConfirm, Is.True, _editor.HintText);
            Assert.That(_editor.TryConfirm(out CommandIntent? intent), Is.True);
            Assert.That(intent!.Stops, Is.Empty);
        }

        /// <summary>Removing the middle stop that separated two equal buildings drops the repeat instead of leaving a stop next to itself.</summary>
        [Test]
        public void RemoveStop_ThatLeavesARepeat_DropsTheRepeat()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);
            _editor.AddStop(Cottage);
            _editor.AddStop(Sawmill);
            _editor.AddStop(Station);
            _editor.RemoveStop(1);

            Assert.That(_editor.Lines, Has.Count.EqualTo(2));
            Assert.That(_editor.CanConfirm, Is.True, _editor.HintText);
        }

        /// <summary>A stop moves earlier or later; a move at the end of the list does nothing.</summary>
        [Test]
        public void MoveStop_ReordersTheStops()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);
            _editor.AddStop(Cottage);
            string first = _editor.Lines[0].Title.Substring(3);

            _editor.MoveStop(0, 1);

            Assert.That(_editor.Lines[1].Title, Is.EqualTo("2. " + first));
            _editor.MoveStop(1, 1);
            Assert.That(_editor.Lines[1].Title, Is.EqualTo("2. " + first), "already last");
        }

        /// <summary>Toggling flips take and put; the item moves to a different good; the amount walks the presets and skips locked modes.</summary>
        [Test]
        public void Actions_ToggleCycleItemAndAmount()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);

            _editor.ToggleKind(0, 0);
            string afterToggle = _editor.Lines[0].Actions[0];
            Assert.That(afterToggle, Does.StartWith("Put "));

            _editor.CycleItem(0, 0);
            Assert.That(_editor.Lines[0].Actions[0], Is.Not.EqualTo(afterToggle));
            Assert.That(_editor.Lines[0].Actions[0], Does.StartWith("Put "), "the kind stays");

            var seen = new HashSet<string>();
            for (int i = 0; i < 8; i++)
            {
                _editor.CycleAmount(0, 0);
                seen.Add(_editor.Lines[0].Actions[0]);
            }

            Assert.That(seen, Has.Count.EqualTo(_content.AmountPresets.Count), "only the presets while percentages and exact numbers are locked");
            Assert.That(string.Join(" ", seen), Does.Not.Contain("%"));
            Assert.That(_editor.AmountNote, Does.Contain("Percentages need"));
            Assert.That(_editor.AmountNote, Does.Contain("Exact numbers need"));
        }

        /// <summary>A stop holds the content's number of actions at most, and never fewer than one.</summary>
        [Test]
        public void Actions_AreLimitedAtBothEnds()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);

            _editor.RemoveAction(0, 0);
            Assert.That(_editor.NoteText, Is.Not.Empty, "the last action cannot be removed");
            Assert.That(_editor.Lines[0].Actions, Has.Count.EqualTo(1));

            for (int i = 0; i < _content.Rules.MaxStopActions + 2; i++)
            {
                _editor.AddAction(0);
            }

            Assert.That(_editor.Lines[0].Actions, Has.Count.EqualTo(_content.Rules.MaxStopActions));
            Assert.That(_editor.Lines[0].Actions[1], Does.StartWith("Put "), "the next action is the opposite kind");
            _editor.RemoveAction(0, 1);
            Assert.That(_editor.Lines[0].Actions, Has.Count.EqualTo(_content.Rules.MaxStopActions - 1));
        }

        /// <summary>A saved route with a percentage amount, while the research is not done, cannot be saved again and says why.</summary>
        [Test]
        public void Save_WithALockedAmount_NamesTheResearchReason()
        {
            var route = new Route(Station, 1, new[]
            {
                new RouteStop(Sawmill, new[] { new RouteAction(RouteActionKind.Take, new ItemId("logs"), Amount.Percent(50)) }),
                new RouteStop(Cottage, new[] { new RouteAction(RouteActionKind.Put, new ItemId("logs"), Amount.Percent(50)) }),
            });
            _editor.Update(City(couriers: 1, saved: route));
            _editor.Open(Station, 1);
            _editor.ToggleKind(0, 0);

            Assert.That(_editor.CanConfirm, Is.False);
            Assert.That(_editor.HintText, Is.EqualTo(RouteEditorModel.AmountModeLockedReason));
        }

        /// <summary>A client that may not send turns Save off with the connection's reason, first of all.</summary>
        [Test]
        public void Save_WhileOffline_IsOffWithTheConnectionReason()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);
            _editor.AddStop(Cottage);

            _editor.SetCommandAvailability(CommandAvailability.For(ConnectionState.Offline));

            Assert.That(_editor.CanConfirm, Is.False);
            Assert.That(_editor.HintText, Is.EqualTo(CommandAvailability.OfflineReason));
        }

        /// <summary>Confirming builds a set-route intent with every stop and action; success closes the editor, failure returns to editing.</summary>
        [Test]
        public void TryConfirm_BuildsTheIntentAndFinishCommandClosesOrReturns()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);
            _editor.AddStop(Cottage);

            Assert.That(_editor.TryConfirm(out CommandIntent? intent), Is.True);

            Assert.That(intent!.Kind, Is.EqualTo(CommandKind.SetRoute));
            Assert.That(intent.BuildingId, Is.EqualTo(Station));
            Assert.That(intent.Slot, Is.EqualTo(1));
            Assert.That(intent.Stops.Count, Is.EqualTo(2));
            Assert.That(intent.Stops[0].BuildingId, Is.EqualTo(Sawmill));
            Assert.That(intent.Stops[0].Actions.Count, Is.EqualTo(1));
            Assert.That(_editor.Phase, Is.EqualTo(RouteEditorPhase.Sending));
            Assert.That(_editor.CanConfirm, Is.False);
            Assert.That(_editor.TryConfirm(out _), Is.False, "no second send while one is on its way");

            _editor.FinishCommand(false);
            Assert.That(_editor.Phase, Is.EqualTo(RouteEditorPhase.Editing));
            Assert.That(_editor.CanConfirm, Is.True, _editor.HintText);

            _editor.TryConfirm(out _);
            _editor.FinishCommand(true);
            Assert.That(_editor.Phase, Is.EqualTo(RouteEditorPhase.Idle));
            Assert.That(_editor.Path, Is.Empty);
        }

        /// <summary>With two couriers the slot button moves to the other slot and its saved route, and back round to the first.</summary>
        [Test]
        public void CycleSlot_MovesBetweenTheStationsCouriers()
        {
            _editor.Update(City(couriers: 2, saved: SavedRoute()));
            _editor.Open(Station, 1);

            Assert.That(_editor.HasSeveralSlots, Is.True);
            _editor.CycleSlot();

            Assert.That(_editor.Slot, Is.EqualTo(2));
            Assert.That(_editor.Lines, Is.Empty, "slot 2 has no route");
            _editor.CycleSlot();
            Assert.That(_editor.Slot, Is.EqualTo(1));
            Assert.That(_editor.Lines, Has.Count.EqualTo(2));
        }

        /// <summary>Cancel closes the editor and clears everything it showed.</summary>
        [Test]
        public void Cancel_ClosesAndClears()
        {
            OpenEmpty();
            _editor.AddStop(Sawmill);
            _editor.AddStop(Cottage);

            _editor.Cancel();

            Assert.That(_editor.Phase, Is.EqualTo(RouteEditorPhase.Idle));
            Assert.That(_editor.Lines, Is.Empty);
            Assert.That(_editor.Path, Is.Empty);
            Assert.That(_editor.CanConfirm, Is.False);
        }

        /// <summary>Opens slot 1 of the station of a city with one courier and no route.</summary>
        private void OpenEmpty()
        {
            _editor.Update(City(couriers: 1));
            Assert.That(_editor.Open(Station, 1), Is.True);
        }

        /// <summary>A saved route of slot 1: take logs at the sawmill, put them into the cottage.</summary>
        private static Route SavedRoute()
        {
            return new Route(Station, 1, new[]
            {
                new RouteStop(Sawmill, new[] { new RouteAction(RouteActionKind.Take, new ItemId("logs"), Amount.Preset(new AmountPresetId("all"))) }),
                new RouteStop(Cottage, new[] { new RouteAction(RouteActionKind.Put, new ItemId("logs"), Amount.Preset(new AmountPresetId("all"))) }),
            });
        }

        /// <summary>
        /// A city with the courier station (id 1, at (0,0)), a sawmill holding logs (id 2, at (4,0)), two cottages (ids 3 and 4, at (0,5) and
        /// (0,6)) and, unless <paramref name="scriptorium"/> is false, a built Scriptorium (id 5, at (6,7)), which routes need (CR-0008 D15);
        /// <paramref name="couriers"/> couriers at the station, when given <paramref name="saved"/> as slot 1's route, and the delivery
        /// <paramref name="lines"/>.
        /// </summary>
        private static CityState City(int couriers, Route? saved = null, bool scriptorium = true, DeliveryLine[]? lines = null)
        {
            var buildings = new[]
            {
                new Building(Station, new BuildingTypeId("courier_station"), 0, 0, 1, couriers, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(
                    Sawmill, new BuildingTypeId("sawmill"), 4, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0,
                    new[] { new KeyValuePair<ItemId, long>(new ItemId("logs"), 5) }),
                new Building(Cottage, new BuildingTypeId("house"), 0, 5, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(Cottage2, new BuildingTypeId("house"), 0, 6, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            };
            var all = new List<Building>(buildings);
            if (scriptorium)
            {
                all.Add(new Building(5, new BuildingTypeId("scriptorium"), 6, 7, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0));
            }

            var list = new List<Courier>();
            for (int slot = 1; slot <= couriers; slot++)
            {
                list.Add(new Courier(Station, slot, CourierState.AtStation, 0, null, null, 600));
            }

            return new CityState(
                0, 1, 10, 700, all, Array.Empty<ResearchId>(), null, null, null, 1, list, saved == null ? Array.Empty<Route>() : new[] { saved },
                lines, lines == null || lines.Length == 0 ? 1 : 10);
        }
    }
}
