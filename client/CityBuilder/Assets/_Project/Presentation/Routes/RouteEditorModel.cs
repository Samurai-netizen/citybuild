#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Routes
{
    /// <summary>
    /// The state of the courier route editor (CR-0008-T2 part C): which courier station and slot the player edits, the stops in order with
    /// their take and put actions (item and amount), the walk time of every leg and of the whole lap, the path drawn on the map, and the words
    /// and enable rule of the Save button. The refusals follow <see cref="CourierRules.SetRoute"/>: the connection's
    /// <see cref="CommandAvailability"/> first, then the number of stops, then a locked amount mode. Pure C# with no Unity objects, so EditMode
    /// tests check every transition and reason.
    /// </summary>
    public sealed class RouteEditorModel
    {
        /// <summary>Shown when the route has exactly one stop.</summary>
        public const string NeedsTwoStopsReason = "A route needs at least 2 stops";

        /// <summary>Shown when the lap closes on the building it started at.</summary>
        public const string LastIsFirstReason = "The last stop is the first building again: remove or move one";

        /// <summary>Shown when a stop's building is gone.</summary>
        public const string MissingBuildingReason = "A stop's building is gone: remove that stop";

        /// <summary>Shown when an amount needs a research that is not done.</summary>
        public const string AmountModeLockedReason = "Research needed for that amount";

        /// <summary>Shown when the route is the one already saved.</summary>
        public const string NoChangesReason = "No changes to save";

        /// <summary>The game content.</summary>
        private readonly GameContent _content;

        /// <summary>The city grid.</summary>
        private readonly CityGrid _grid;

        /// <summary>The reusable path finder for the grid.</summary>
        private readonly PathFinder _finder;

        /// <summary>The stops being edited, in order.</summary>
        private readonly List<RouteStopEdit> _stops = new List<RouteStopEdit>();

        /// <summary>The words of the stops, rebuilt after every change.</summary>
        private readonly List<RouteStopLine> _lines = new List<RouteStopLine>();

        /// <summary>The amounts a tap on the amount button cycles through, rebuilt from the content and the research done.</summary>
        private readonly List<Amount> _amounts = new List<Amount>();

        /// <summary>The displayed city.</summary>
        private CityState? _city;

        /// <summary>Whether commands may be sent right now.</summary>
        private CommandAvailability _availability = CommandAvailability.Allowed;

        /// <summary>Creates a route editor over <paramref name="content"/> and <paramref name="grid"/>.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public RouteEditorModel(GameContent content, CityGrid grid)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _finder = new PathFinder(content);
        }

        /// <summary>Raised after any change of the state or the words.</summary>
        public event Action? Changed;

        /// <summary>Where the editor is.</summary>
        public RouteEditorPhase Phase { get; private set; } = RouteEditorPhase.Idle;

        /// <summary>The courier station being edited; 0 when idle.</summary>
        public int StationId { get; private set; }

        /// <summary>The courier slot being edited (1 to the station's couriers); 0 when idle.</summary>
        public int Slot { get; private set; }

        /// <summary>The stops in order, with their words.</summary>
        public IReadOnlyList<RouteStopLine> Lines => _lines;

        /// <summary>The path of the whole lap across the city, tile by tile, for the map (display only).</summary>
        public IReadOnlyList<(int X, int Y)> Path { get; private set; } = Array.Empty<(int X, int Y)>();

        /// <summary>The seconds of one lap, the sum of every leg's walk; 0 with fewer than 2 stops.</summary>
        public long LapSeconds { get; private set; }

        /// <summary>The heading, for example <c>Courier 1 of Courier station</c>.</summary>
        public string TitleText { get; private set; } = string.Empty;

        /// <summary>The summary line, for example <c>3 stops · lap 4m 20s</c>, or what to do next when the route is empty.</summary>
        public string SummaryText { get; private set; } = string.Empty;

        /// <summary>The note on amounts: which research unlocks percentages and exact numbers; empty when both are unlocked.</summary>
        public string AmountNote { get; private set; } = string.Empty;

        /// <summary>The last refused edit (for example a stop twice in a row), cleared by the next accepted edit.</summary>
        public string NoteText { get; private set; } = string.Empty;

        /// <summary>Why Save is off, or empty when it works.</summary>
        public string HintText { get; private set; } = string.Empty;

        /// <summary>The Save button's words: <c>Save route</c>, or <c>Clear route</c> with no stops.</summary>
        public string ConfirmText { get; private set; } = "Save route";

        /// <summary>Whether the Save button works.</summary>
        public bool CanConfirm { get; private set; }

        /// <summary>Whether the station has more than one courier slot, so the slot button shows.</summary>
        public bool HasSeveralSlots { get; private set; }

        /// <summary>Whether another stop can still be added (the content's route limit).</summary>
        public bool CanAddStop => _stops.Count < _content.Rules.MaxRouteStops;

        /// <summary>Updates the displayed city and refreshes the words and availability.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Update(CityState city)
        {
            _city = city ?? throw new ArgumentNullException(nameof(city));
            RefreshAmounts();
            if (Phase != RouteEditorPhase.Idle)
            {
                Rebuild();
                Changed?.Invoke();
            }
        }

        /// <summary>Sets whether commands are allowed by the connection (<see cref="CommandAvailability.For"/>).</summary>
        public void SetCommandAvailability(CommandAvailability availability)
        {
            if (_availability.Equals(availability))
            {
                return;
            }

            _availability = availability;
            if (Phase != RouteEditorPhase.Idle)
            {
                Rebuild();
                Changed?.Invoke();
            }
        }

        /// <summary>Opens the editor on <paramref name="slot"/> of the courier station <paramref name="stationId"/> with the route the city holds.</summary>
        /// <returns>True when the station is a built courier station with a courier in that slot.</returns>
        public bool Open(int stationId, int slot)
        {
            if (_city == null || !IsEditableSlot(stationId, slot))
            {
                return false;
            }

            StationId = stationId;
            Slot = slot;
            LoadSaved();
            Phase = RouteEditorPhase.Editing;
            NoteText = string.Empty;
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Switches to the station's next courier slot, with its saved route (drops the edits of the slot left).</summary>
        public void CycleSlot()
        {
            if (Phase != RouteEditorPhase.Editing || _city == null || !_city.TryGetBuilding(StationId, out Building? station))
            {
                return;
            }

            int next = Slot % station!.AssignedWorkers + 1;
            if (next == Slot || !IsEditableSlot(StationId, next))
            {
                return;
            }

            Slot = next;
            LoadSaved();
            NoteText = string.Empty;
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>Adds the building on <paramref name="tile"/> as the last stop; a tile without a building changes nothing.</summary>
        public void SelectTile(GridCoordinate? tile)
        {
            if (Phase != RouteEditorPhase.Editing || _city == null || !tile.HasValue || !_grid.Contains(tile.Value))
            {
                return;
            }

            if (_city.TryGetBuildingAt(tile.Value.X, tile.Value.Y, out Building? found) && found != null)
            {
                AddStop(found.Id);
            }
        }

        /// <summary>Adds building <paramref name="buildingId"/> as the last stop with a first action chosen from its store.</summary>
        /// <returns>False, with <see cref="NoteText"/> set, when the route is full, the building is not built or it would repeat its neighbour.</returns>
        public bool AddStop(int buildingId)
        {
            if (Phase != RouteEditorPhase.Editing || _city == null || !_city.TryGetBuilding(buildingId, out Building? building)
                || building!.Phase == BuildingPhase.Constructing)
            {
                return Refuse("Only finished buildings can be a stop");
            }

            if (!CanAddStop)
            {
                return Refuse("A route has at most " + Number(_content.Rules.MaxRouteStops) + " stops");
            }

            if (_stops.Count > 0 && _stops[_stops.Count - 1].BuildingId == buildingId)
            {
                return Refuse("A stop cannot follow itself");
            }

            var stop = new RouteStopEdit(buildingId);
            stop.Actions.Add(DefaultAction(building));
            _stops.Add(stop);
            return Accept();
        }

        /// <summary>Removes the stop at <paramref name="index"/>; if that leaves the same building twice in a row, the repeat goes too.</summary>
        public void RemoveStop(int index)
        {
            if (!HasStop(index))
            {
                return;
            }

            _stops.RemoveAt(index);
            // Removing a stop can make two equal buildings neighbours: drop the later of the pair. (The last and the first being equal is
            // not repaired here: Save names it, and the player decides which to remove.)
            for (int i = _stops.Count - 1; i >= 1; i--)
            {
                if (_stops[i].BuildingId == _stops[i - 1].BuildingId)
                {
                    _stops.RemoveAt(i);
                }
            }

            Accept();
        }

        /// <summary>Moves the stop at <paramref name="index"/> by <paramref name="delta"/> places (−1 earlier, +1 later); a move that would repeat a neighbour is refused.</summary>
        public void MoveStop(int index, int delta)
        {
            int target = index + delta;
            if (!HasStop(index) || target < 0 || target >= _stops.Count || target == index)
            {
                return;
            }

            RouteStopEdit moved = _stops[index];
            _stops.RemoveAt(index);
            _stops.Insert(target, moved);
            if (HasRepeatedNeighbour())
            {
                _stops.RemoveAt(target);
                _stops.Insert(index, moved);
                Refuse("A stop cannot sit next to the same building");
                return;
            }

            Accept();
        }

        /// <summary>Adds an action to the stop at <paramref name="stopIndex"/> (the content's limit per stop), copying the kind opposite to the last one.</summary>
        public void AddAction(int stopIndex)
        {
            if (!HasStop(stopIndex) || _city == null)
            {
                return;
            }

            RouteStopEdit stop = _stops[stopIndex];
            if (stop.Actions.Count >= _content.Rules.MaxStopActions)
            {
                Refuse("A stop has at most " + Number(_content.Rules.MaxStopActions) + " actions");
                return;
            }

            RouteActionEdit last = stop.Actions[stop.Actions.Count - 1];
            stop.Actions.Add(new RouteActionEdit(
                last.Kind == RouteActionKind.Take ? RouteActionKind.Put : RouteActionKind.Take, last.Item, last.Amount));
            Accept();
        }

        /// <summary>Removes an action; a stop keeps at least one (remove the stop instead).</summary>
        public void RemoveAction(int stopIndex, int actionIndex)
        {
            if (!HasAction(stopIndex, actionIndex))
            {
                return;
            }

            if (_stops[stopIndex].Actions.Count == 1)
            {
                Refuse("A stop needs an action: remove the stop instead");
                return;
            }

            _stops[stopIndex].Actions.RemoveAt(actionIndex);
            Accept();
        }

        /// <summary>Flips an action between take and put.</summary>
        public void ToggleKind(int stopIndex, int actionIndex)
        {
            if (!HasAction(stopIndex, actionIndex))
            {
                return;
            }

            RouteActionEdit action = _stops[stopIndex].Actions[actionIndex];
            action.Kind = action.Kind == RouteActionKind.Take ? RouteActionKind.Put : RouteActionKind.Take;
            Accept();
        }

        /// <summary>Moves an action's item to the next item of the content (the currency is skipped).</summary>
        public void CycleItem(int stopIndex, int actionIndex)
        {
            if (!HasAction(stopIndex, actionIndex))
            {
                return;
            }

            RouteActionEdit action = _stops[stopIndex].Actions[actionIndex];
            var items = new List<ItemId>();
            for (int i = 0; i < _content.Items.Count; i++)
            {
                if (_content.Items[i].Id != _content.Currency)
                {
                    items.Add(_content.Items[i].Id);
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            int at = items.IndexOf(action.Item);
            action.Item = items[(at + 1) % items.Count];
            Accept();
        }

        /// <summary>Moves an action's amount to the next one: the presets, then the percentages and exact numbers once their research is done.</summary>
        public void CycleAmount(int stopIndex, int actionIndex)
        {
            if (!HasAction(stopIndex, actionIndex) || _amounts.Count == 0)
            {
                return;
            }

            RouteActionEdit action = _stops[stopIndex].Actions[actionIndex];
            int at = -1;
            for (int i = 0; i < _amounts.Count; i++)
            {
                if (_amounts[i].Equals(action.Amount))
                {
                    at = i;
                    break;
                }
            }

            action.Amount = _amounts[(at + 1) % _amounts.Count];
            Accept();
        }

        /// <summary>Drops every edit and loads the route the city holds again.</summary>
        public void Revert()
        {
            if (Phase != RouteEditorPhase.Editing)
            {
                return;
            }

            LoadSaved();
            Accept();
        }

        /// <summary>Confirms the route and returns the <see cref="CommandIntent"/> to send when <see cref="CanConfirm"/> is true.</summary>
        public bool TryConfirm(out CommandIntent? intent)
        {
            if (Phase != RouteEditorPhase.Editing || !CanConfirm)
            {
                intent = null;
                return false;
            }

            var stops = new List<RouteStopRequestDto>(_stops.Count);
            for (int i = 0; i < _stops.Count; i++)
            {
                var actions = new List<RouteActionRequestDto>(_stops[i].Actions.Count);
                for (int j = 0; j < _stops[i].Actions.Count; j++)
                {
                    RouteActionEdit action = _stops[i].Actions[j];
                    actions.Add(new RouteActionRequestDto(
                        action.Kind == RouteActionKind.Take ? RouteActionKindDto.Take : RouteActionKindDto.Put, action.Item.Value, action.Amount.ToString()));
                }

                stops.Add(new RouteStopRequestDto(_stops[i].BuildingId, actions));
            }

            Phase = RouteEditorPhase.Sending;
            intent = CommandIntent.SetRoute(StationId, Slot, stops);
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Called when the sent command finishes: closes the editor on success, or returns to editing on failure.</summary>
        public void FinishCommand(bool succeeded)
        {
            if (Phase != RouteEditorPhase.Sending)
            {
                return;
            }

            if (succeeded)
            {
                Cancel();
                return;
            }

            Phase = RouteEditorPhase.Editing;
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>Leaves the editor and clears the path preview.</summary>
        public void Cancel()
        {
            if (Phase == RouteEditorPhase.Idle)
            {
                return;
            }

            Phase = RouteEditorPhase.Idle;
            StationId = 0;
            Slot = 0;
            _stops.Clear();
            _lines.Clear();
            Path = Array.Empty<(int X, int Y)>();
            LapSeconds = 0;
            TitleText = string.Empty;
            SummaryText = string.Empty;
            NoteText = string.Empty;
            HintText = string.Empty;
            CanConfirm = false;
            Changed?.Invoke();
        }

        /// <summary>Whether <paramref name="slot"/> of <paramref name="stationId"/> is a built courier station's slot that has a courier.</summary>
        private bool IsEditableSlot(int stationId, int slot)
        {
            if (_city == null || !_city.TryGetBuilding(stationId, out Building? station) || station!.Phase == BuildingPhase.Constructing
                || !_content.TryGetBuilding(station.TypeId, out BuildingDefinition? definition) || definition!.Kind != BuildingKind.CourierStation)
            {
                return false;
            }

            for (int i = 0; i < _city.Couriers.Count; i++)
            {
                if (_city.Couriers[i].StationId == stationId && _city.Couriers[i].Slot == slot)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Replaces the edited stops with the route the city holds for the current slot (none: an empty list).</summary>
        private void LoadSaved()
        {
            _stops.Clear();
            if (_city != null && _city.TryGetRoute(StationId, Slot, out Route? route))
            {
                for (int i = 0; i < route!.Stops.Count; i++)
                {
                    var stop = new RouteStopEdit(route.Stops[i].BuildingId);
                    for (int j = 0; j < route.Stops[i].Actions.Count; j++)
                    {
                        RouteAction action = route.Stops[i].Actions[j];
                        stop.Actions.Add(new RouteActionEdit(action.Kind, action.Item, action.Amount));
                    }

                    _stops.Add(stop);
                }
            }
        }

        /// <summary>Whether the edited stops equal the route the city holds for the current slot.</summary>
        private bool EqualsSaved()
        {
            if (_city == null || !_city.TryGetRoute(StationId, Slot, out Route? route))
            {
                return _stops.Count == 0;
            }

            if (route!.Stops.Count != _stops.Count)
            {
                return false;
            }

            for (int i = 0; i < _stops.Count; i++)
            {
                RouteStop saved = route.Stops[i];
                if (saved.BuildingId != _stops[i].BuildingId || saved.Actions.Count != _stops[i].Actions.Count)
                {
                    return false;
                }

                for (int j = 0; j < saved.Actions.Count; j++)
                {
                    RouteAction left = saved.Actions[j];
                    RouteActionEdit right = _stops[i].Actions[j];
                    if (left.Kind != right.Kind || left.Item != right.Item || !left.Amount.Equals(right.Amount))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Whether two stops in a row are at one building (the lap's closing pair is <see cref="LastIsFirst"/>).</summary>
        private bool HasRepeatedNeighbour()
        {
            for (int i = 1; i < _stops.Count; i++)
            {
                if (_stops[i].BuildingId == _stops[i - 1].BuildingId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the lap closes on the same building (the last stop is the first one again), which the server refuses.</summary>
        private bool LastIsFirst() => _stops.Count >= 2 && _stops[_stops.Count - 1].BuildingId == _stops[0].BuildingId;

        /// <summary>The first action of a new stop: take the first good in the building's store, else put the first good it accepts, all of it.</summary>
        private RouteActionEdit DefaultAction(Building building)
        {
            Amount all = _amounts.Count > 0 ? _amounts[0] : Amount.Preset(new AmountPresetId("all"));
            ItemId? firstItem = null;
            for (int i = 0; i < _content.Items.Count; i++)
            {
                ItemId id = _content.Items[i].Id;
                if (id == _content.Currency)
                {
                    continue;
                }

                firstItem ??= id;
                if (building.GetStock(id) > 0)
                {
                    return new RouteActionEdit(RouteActionKind.Take, id, all);
                }
            }

            for (int i = 0; i < _content.Items.Count; i++)
            {
                ItemId id = _content.Items[i].Id;
                if (id != _content.Currency && CityQueries.Accepts(_city!, _content, building, id))
                {
                    return new RouteActionEdit(RouteActionKind.Put, id, all);
                }
            }

            return new RouteActionEdit(RouteActionKind.Take, firstItem ?? new ItemId("logs"), all);
        }

        /// <summary>Rebuilds the list of amounts a tap cycles through, and the note on the locked ones.</summary>
        private void RefreshAmounts()
        {
            _amounts.Clear();
            var presets = new List<AmountPresetDefinition>(_content.AmountPresets);
            presets.Sort((left, right) => right.Percent.CompareTo(left.Percent));
            for (int i = 0; i < presets.Count; i++)
            {
                _amounts.Add(Amount.Preset(presets[i].Id));
            }

            bool percent = _city != null && CityQueries.IsAmountModeUnlocked(_city, _content, AmountMode.Percent);
            bool exact = _city != null && CityQueries.IsAmountModeUnlocked(_city, _content, AmountMode.Exact);
            if (percent)
            {
                _amounts.Add(Amount.Percent(25));
                _amounts.Add(Amount.Percent(50));
                _amounts.Add(Amount.Percent(75));
            }

            if (exact)
            {
                _amounts.Add(Amount.Exact(10));
                _amounts.Add(Amount.Exact(50));
                _amounts.Add(Amount.Exact(100));
            }

            var notes = new List<string>();
            if (!percent)
            {
                notes.Add("Percentages need " + UnlockingResearch(AmountMode.Percent));
            }

            if (!exact)
            {
                notes.Add("Exact numbers need " + UnlockingResearch(AmountMode.Exact));
            }

            AmountNote = string.Join(" · ", notes);
        }

        /// <summary>The name of the research that unlocks <paramref name="mode"/>, or <c>research</c> when the content has none.</summary>
        private string UnlockingResearch(AmountMode mode)
        {
            for (int i = 0; i < _content.Research.Count; i++)
            {
                IReadOnlyList<ResearchEffect> effects = _content.Research[i].Effects;
                for (int j = 0; j < effects.Count; j++)
                {
                    if (effects[j] is UnlockAmountModeEffect unlock && unlock.Mode == mode)
                    {
                        return _content.Research[i].Name;
                    }
                }
            }

            return "research";
        }

        /// <summary>Recomputes the words, the path and the Save button from the edited stops.</summary>
        private void Rebuild()
        {
            _lines.Clear();
            if (_city == null || !_city.TryGetBuilding(StationId, out Building? station))
            {
                Path = Array.Empty<(int X, int Y)>();
                LapSeconds = 0;
                CanConfirm = false;
                return;
            }

            TitleText = "Courier " + Number(Slot) + " of " + BuildingNameOf(station!);
            HasSeveralSlots = station!.AssignedWorkers > 1;
            _finder.Load(_city);
            int bonus = CityQueries.WalkBonusPermille(_city, _content);
            var path = new List<(int X, int Y)>();
            long lap = 0;
            bool missing = false;
            bool locked = false;
            for (int i = 0; i < _stops.Count; i++)
            {
                RouteStopEdit stop = _stops[i];
                bool exists = _city.TryGetBuilding(stop.BuildingId, out Building? building);
                missing |= !exists;
                string walk = string.Empty;
                if (exists && _stops.Count >= 2)
                {
                    RouteStopEdit next = _stops[(i + 1) % _stops.Count];
                    if (_city.TryGetBuilding(next.BuildingId, out Building? nextBuilding))
                    {
                        long seconds = _finder.Measure(building!.X, building.Y, nextBuilding!.X, nextBuilding.Y).Seconds(bonus);
                        lap += seconds;
                        path.AddRange(_finder.Route(building.X, building.Y, nextBuilding.X, nextBuilding.Y));
                        walk = "Next stop in " + DurationText.Format(seconds);
                    }
                }

                var actions = new List<string>(stop.Actions.Count);
                var parts = new List<RouteActionLine>(stop.Actions.Count);
                for (int j = 0; j < stop.Actions.Count; j++)
                {
                    RouteActionEdit action = stop.Actions[j];
                    locked |= action.Amount.Mode.HasValue && !CityQueries.IsAmountModeUnlocked(_city, _content, action.Amount.Mode.Value);
                    var part = new RouteActionLine(
                        action.Kind == RouteActionKind.Take ? "Take" : "Put", ItemText.ItemName(_content, action.Item), AmountLabel(action.Amount));
                    parts.Add(part);
                    actions.Add(part.KindText + " " + part.ItemText + " · " + part.AmountText);
                }

                _lines.Add(new RouteStopLine(
                    Number(i + 1) + ". " + (exists ? BuildingNameOf(building!) : "Missing building"), walk, actions, parts));
            }

            Path = path;
            LapSeconds = lap;
            ConfirmText = _stops.Count == 0 ? "Clear route" : "Save route";
            SummaryText = _stops.Count == 0
                ? "No route: tap buildings on the map to add stops"
                : Number(_stops.Count) + (_stops.Count == 1 ? " stop" : " stops") + (_stops.Count >= 2 ? " · lap " + DurationText.Format(lap) : string.Empty);
            HintText = Phase == RouteEditorPhase.Sending ? string.Empty : ReasonOff(missing, locked);
            CanConfirm = Phase == RouteEditorPhase.Editing && HintText.Length == 0;
        }

        /// <summary>The first reason Save is off in <see cref="CourierRules.SetRoute"/> order, or empty when it works.</summary>
        private string ReasonOff(bool missing, bool locked)
        {
            if (!_availability.IsAllowed)
            {
                return _availability.Reason;
            }

            if (_stops.Count == 1)
            {
                return NeedsTwoStopsReason;
            }

            if (LastIsFirst())
            {
                return LastIsFirstReason;
            }

            if (missing)
            {
                return MissingBuildingReason;
            }

            if (locked)
            {
                return AmountModeLockedReason;
            }

            return EqualsSaved() ? NoChangesReason : string.Empty;
        }

        /// <summary>Whether <paramref name="index"/> is a stop of the edited route and the editor is editing.</summary>
        private bool HasStop(int index) => Phase == RouteEditorPhase.Editing && index >= 0 && index < _stops.Count;

        /// <summary>Whether the stop and the action exist.</summary>
        private bool HasAction(int stopIndex, int actionIndex) =>
            HasStop(stopIndex) && actionIndex >= 0 && actionIndex < _stops[stopIndex].Actions.Count;

        /// <summary>Sets the note of a refused edit and tells the view.</summary>
        /// <returns>Always false, so a caller can return it.</returns>
        private bool Refuse(string note)
        {
            NoteText = note;
            Changed?.Invoke();
            return false;
        }

        /// <summary>Clears the note after an accepted edit, rebuilds and tells the view.</summary>
        /// <returns>Always true, so a caller can return it.</returns>
        private bool Accept()
        {
            NoteText = string.Empty;
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>The display name of <paramref name="building"/> from content.</summary>
        private string BuildingNameOf(Building building)
        {
            return _content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition) ? definition!.Name : building.TypeId.Value;
        }

        /// <summary>Human-readable label for <paramref name="amount"/>: the preset's name, <c>50%</c> or an exact number.</summary>
        private string AmountLabel(Amount amount)
        {
            if (!amount.Mode.HasValue && _content.TryGetAmountPreset(amount.PresetId, out AmountPresetDefinition? preset))
            {
                return preset!.Name;
            }

            return amount.ToString();
        }

        /// <summary>A whole number in text, never in the machine's culture.</summary>
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>One stop being edited: its building and its actions.</summary>
        private sealed class RouteStopEdit
        {
            /// <summary>Starts a stop at <paramref name="buildingId"/> with no actions yet.</summary>
            public RouteStopEdit(int buildingId)
            {
                BuildingId = buildingId;
            }

            /// <summary>The building of the stop.</summary>
            public int BuildingId { get; }

            /// <summary>The stop's actions in order.</summary>
            public List<RouteActionEdit> Actions { get; } = new List<RouteActionEdit>();
        }

        /// <summary>One action being edited: take or put, an item and an amount.</summary>
        private sealed class RouteActionEdit
        {
            /// <summary>Starts an action.</summary>
            public RouteActionEdit(RouteActionKind kind, ItemId item, Amount amount)
            {
                Kind = kind;
                Item = item;
                Amount = amount;
            }

            /// <summary>Take from the stop's building or put into it.</summary>
            public RouteActionKind Kind { get; set; }

            /// <summary>The item moved.</summary>
            public ItemId Item { get; set; }

            /// <summary>How much of it.</summary>
            public Amount Amount { get; set; }
        }
    }

    /// <summary>The words of one stop in the editor: its title, the walk to the next stop, and one text per action.</summary>
    public sealed class RouteStopLine
    {
        /// <summary>Makes a stop line.</summary>
        public RouteStopLine(string title, string walkText, IReadOnlyList<string> actions, IReadOnlyList<RouteActionLine> parts)
        {
            Title = title;
            WalkText = walkText;
            Actions = actions;
            Parts = parts;
        }

        /// <summary>For example <c>1. Lumberjack</c>.</summary>
        public string Title { get; }

        /// <summary>For example <c>Next stop in 1m 10s</c>; empty with fewer than 2 stops.</summary>
        public string WalkText { get; }

        /// <summary>For example <c>Take Logs · All</c>, one text per action.</summary>
        public IReadOnlyList<string> Actions { get; }

        /// <summary>The same actions split into the words of their three buttons, in the same order as <see cref="Actions"/>.</summary>
        public IReadOnlyList<RouteActionLine> Parts { get; }
    }

    /// <summary>The words of one action's three buttons: take or put, the item, and the amount.</summary>
    public sealed class RouteActionLine
    {
        /// <summary>Makes an action line.</summary>
        public RouteActionLine(string kindText, string itemText, string amountText)
        {
            KindText = kindText;
            ItemText = itemText;
            AmountText = amountText;
        }

        /// <summary><c>Take</c> or <c>Put</c>.</summary>
        public string KindText { get; }

        /// <summary>The item's name, for example <c>Logs</c>.</summary>
        public string ItemText { get; }

        /// <summary>The amount: a preset's name, <c>50%</c> or an exact number.</summary>
        public string AmountText { get; }
    }

    /// <summary>Where the route editor is.</summary>
    public enum RouteEditorPhase
    {
        /// <summary>Closed.</summary>
        Idle,

        /// <summary>Open: the player changes stops and actions.</summary>
        Editing,

        /// <summary>Save was pressed and the command is on its way.</summary>
        Sending,
    }
}
