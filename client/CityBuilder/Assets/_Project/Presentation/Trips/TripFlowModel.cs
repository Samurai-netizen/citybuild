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

namespace CityBuilder.Client.Presentation.Trips
{
    /// <summary>
    /// The state of the manual trip flow (CR-0008): which source building the player started from, which target building they tapped on the map,
    /// which item and amount (<c>all</c>, <c>half</c>, <c>quarter</c>, or a percentage once unlocked) to send, the shortest path across the city
    /// via <see cref="PathFinder"/>, and the words and enable rules of the Send button. The disable reasons follow <see cref="TripRules.StartTrip"/>
    /// in the server's order (<c>Target does not need or store this item</c>, <c>No free workers</c>, <c>Nothing to carry</c>) after the connection's
    /// <see cref="CommandAvailability"/>. Pure C# with no Unity objects, so EditMode tests check every transition and refusal reason.
    /// </summary>
    public sealed class TripFlowModel
    {
        /// <summary>The reason shown when the target building neither needs the item for a project nor stores it.</summary>
        public const string TargetDoesNotAcceptReason = "Target does not need or store this item";

        /// <summary>The reason shown when the city has no free worker for a trip.</summary>
        public const string NoFreeWorkersReason = "No free workers";

        /// <summary>The reason shown when the resolved carry quantity at the source store is zero.</summary>
        public const string NothingToCarryReason = "Nothing to carry";

        /// <summary>The reason shown when the chosen amount mode has not been researched yet.</summary>
        public const string AmountModeLockedReason = "Research needed for that amount";

        /// <summary>The game content.</summary>
        private readonly GameContent _content;

        /// <summary>The city grid.</summary>
        private readonly CityGrid _grid;

        /// <summary>The reusable path finder for the grid.</summary>
        private readonly PathFinder _finder;

        /// <summary>The items available to pick in the source building's store.</summary>
        private readonly List<ItemId> _items = new List<ItemId>();

        /// <summary>The displayed city.</summary>
        private CityState? _city;

        /// <summary>Whether commands may be sent right now.</summary>
        private CommandAvailability _availability = CommandAvailability.Allowed;

        /// <summary>Creates a trip flow model over <paramref name="content"/> and <paramref name="grid"/>.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public TripFlowModel(GameContent content, CityGrid grid)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _finder = new PathFinder(content);
            Amount = Amount.Preset(new AmountPresetId("all"));
        }

        /// <summary>Raised whenever the phase, selection, path, or words change.</summary>
        public event Action? Changed;

        /// <summary>Where the player is in the flow.</summary>
        public TripPhase Phase { get; private set; } = TripPhase.Idle;

        /// <summary>The id of the source building whose store the worker takes the good from; null while <see cref="TripPhase.Idle"/>.</summary>
        public int? SourceId { get; private set; }

        /// <summary>The id of the target building on the map; null until the player taps another building.</summary>
        public int? TargetId { get; private set; }

        /// <summary>The item chosen to carry.</summary>
        public ItemId Item { get; private set; }

        /// <summary>The items in the source building's store that the player can cycle through.</summary>
        public IReadOnlyList<ItemId> AvailableItems => _items;

        /// <summary>The chosen amount (<c>all</c>, <c>half</c>, <c>quarter</c>, or a percentage).</summary>
        public Amount Amount { get; private set; }

        /// <summary>Whether the city has researched percentage amounts (<see cref="AmountMode.Percent"/>).</summary>
        public bool IsPercentUnlocked { get; private set; }

        /// <summary>The shortest path from source to target via <see cref="PathFinder.Route"/>; empty until a target is picked.</summary>
        public IReadOnlyList<(int X, int Y)> Path { get; private set; } = Array.Empty<(int X, int Y)>();

        /// <summary>The walk duration in city seconds along <see cref="Path"/>; 0 until a target is picked.</summary>
        public long WalkSeconds { get; private set; }

        /// <summary>How many units the worker will carry on this trip.</summary>
        public long CarryQuantity { get; private set; }

        /// <summary>The main line of the trip bar.</summary>
        public string PromptText { get; private set; } = string.Empty;

        /// <summary>The second line of the trip bar (item, amount, and walk time).</summary>
        public string DetailText { get; private set; } = string.Empty;

        /// <summary>The label on the item button, such as <c>Logs (10)</c>.</summary>
        public string ItemButtonText { get; private set; } = string.Empty;

        /// <summary>Why the Send button is disabled, or empty when <see cref="CanConfirm"/> is true or no target is picked yet.</summary>
        public string HintText { get; private set; } = string.Empty;

        /// <summary>Whether the Send button works right now.</summary>
        public bool CanConfirm { get; private set; }

        /// <summary>Updates the displayed city and refreshes the flow's numbers and availability.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Update(CityState city)
        {
            _city = city ?? throw new ArgumentNullException(nameof(city));
            IsPercentUnlocked = CityQueries.IsAmountModeUnlocked(city, _content, AmountMode.Percent);
            if (Phase != TripPhase.Idle)
            {
                RefreshItems();
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
            if (Phase != TripPhase.Idle)
            {
                Rebuild();
                Changed?.Invoke();
            }
        }

        /// <summary>Starts the trip flow from <paramref name="sourceBuildingId"/>.</summary>
        /// <returns>True when the building exists in the city and holds at least one good in its store.</returns>
        public bool StartFrom(int sourceBuildingId)
        {
            if (_city == null || !_city.TryGetBuilding(sourceBuildingId, out Building? source) || source!.Phase == BuildingPhase.Constructing)
            {
                return false;
            }

            SourceId = sourceBuildingId;
            TargetId = null;
            Amount = Amount.Preset(new AmountPresetId("all"));
            RefreshItems();
            if (_items.Count == 0)
            {
                SourceId = null;
                return false;
            }

            Item = _items[0];
            Phase = TripPhase.PickingTarget;
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Picks the target building at <paramref name="tile"/> on the map (or clears the target if the tile has no other building).</summary>
        public void SelectTile(GridCoordinate? tile)
        {
            if (Phase != TripPhase.PickingTarget && Phase != TripPhase.Confirming)
            {
                return;
            }

            int? targetId = null;
            if (_city != null && tile.HasValue && _grid.Contains(tile.Value))
            {
                if (_city.TryGetBuildingAt(tile.Value.X, tile.Value.Y, out Building? found) && found != null && found.Id != SourceId)
                {
                    targetId = found.Id;
                }
            }

            TargetId = targetId;
            Phase = targetId.HasValue ? TripPhase.Confirming : TripPhase.PickingTarget;
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>Selects <paramref name="item"/> to carry on the trip.</summary>
        public void SetItem(ItemId item)
        {
            if (Phase != TripPhase.PickingTarget && Phase != TripPhase.Confirming)
            {
                return;
            }

            Item = item;
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>Cycles to the next item in the source building's store.</summary>
        public void CycleItem()
        {
            if ((Phase != TripPhase.PickingTarget && Phase != TripPhase.Confirming) || _items.Count <= 1)
            {
                return;
            }

            int index = _items.IndexOf(Item);
            Item = _items[(index + 1) % _items.Count];
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>Selects <paramref name="amount"/> to carry on the trip.</summary>
        public void SetAmount(Amount amount)
        {
            if (Phase != TripPhase.PickingTarget && Phase != TripPhase.Confirming)
            {
                return;
            }

            Amount = amount;
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>Selects a preset amount (<c>all</c>, <c>half</c>, or <c>quarter</c>).</summary>
        public void SetPreset(string presetId)
        {
            SetAmount(Amount.Preset(new AmountPresetId(presetId)));
        }

        /// <summary>Selects or cycles a percentage amount (<c>25%</c>, <c>50%</c>, <c>75%</c>, <c>100%</c>).</summary>
        public void CyclePercent()
        {
            if (Phase != TripPhase.PickingTarget && Phase != TripPhase.Confirming)
            {
                return;
            }

            int next = Amount.Mode == AmountMode.Percent
                ? (Amount.Value >= 100 ? 25 : (int)Amount.Value + 25)
                : 50;
            SetAmount(Amount.Percent(next));
        }

        /// <summary>Confirms the trip and returns the <see cref="CommandIntent"/> to send when <see cref="CanConfirm"/> is true.</summary>
        public bool TryConfirm(out CommandIntent? intent)
        {
            if (Phase != TripPhase.Confirming || !CanConfirm || !SourceId.HasValue || !TargetId.HasValue)
            {
                intent = null;
                return false;
            }

            Phase = TripPhase.Sending;
            intent = CommandIntent.StartTrip(SourceId.Value, TargetId.Value, Item.Value, Amount.ToString());
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Called when the sent command finishes: closes the flow on success, or returns to <see cref="TripPhase.Confirming"/> on failure.</summary>
        public void FinishCommand(bool succeeded)
        {
            if (Phase != TripPhase.Sending)
            {
                return;
            }

            if (succeeded)
            {
                Cancel();
                return;
            }

            Phase = TargetId.HasValue ? TripPhase.Confirming : TripPhase.PickingTarget;
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>Leaves the trip flow and clears the path preview.</summary>
        public void Cancel()
        {
            if (Phase == TripPhase.Idle)
            {
                return;
            }

            Phase = TripPhase.Idle;
            SourceId = null;
            TargetId = null;
            Path = Array.Empty<(int X, int Y)>();
            WalkSeconds = 0;
            CarryQuantity = 0;
            PromptText = string.Empty;
            DetailText = string.Empty;
            ItemButtonText = string.Empty;
            HintText = string.Empty;
            CanConfirm = false;
            Changed?.Invoke();
        }

        /// <summary>Collects the items held in the source building's store.</summary>
        private void RefreshItems()
        {
            _items.Clear();
            if (_city == null || !SourceId.HasValue || !_city.TryGetBuilding(SourceId.Value, out Building? source))
            {
                return;
            }

            for (int i = 0; i < _content.Items.Count; i++)
            {
                ItemDefinition item = _content.Items[i];
                if (item.Id == _content.Currency)
                {
                    continue;
                }

                if (source!.GetStock(item.Id) > 0 || CityQueries.TryGetEffectiveLimit(_city, _content, source, item.Id, out _))
                {
                    _items.Add(item.Id);
                }
            }

            if (_items.Count > 0 && !_items.Contains(Item))
            {
                // Prefer the first item with positive stock.
                Item = _items[0];
                for (int i = 0; i < _items.Count; i++)
                {
                    if (source!.GetStock(_items[i]) > 0)
                    {
                        Item = _items[i];
                        break;
                    }
                }
            }
        }

        /// <summary>Recomputes the path, walk duration, carry quantity, texts, and button enable state.</summary>
        private void Rebuild()
        {
            if (_city == null || !SourceId.HasValue || !_city.TryGetBuilding(SourceId.Value, out Building? source))
            {
                Path = Array.Empty<(int X, int Y)>();
                WalkSeconds = 0;
                CarryQuantity = 0;
                PromptText = string.Empty;
                DetailText = string.Empty;
                ItemButtonText = string.Empty;
                HintText = string.Empty;
                CanConfirm = false;
                return;
            }

            long stock = source!.GetStock(Item);
            long wanted = ResolveAmount(Amount, stock);
            CarryQuantity = Math.Min(Math.Min(wanted, stock), _content.Rules.TripCarryLimit);
            string itemName = ItemText.ItemName(_content, Item);
            string sourceName = BuildingNameOf(source);
            string amountLabel = FormatAmountLabel(Amount);
            ItemButtonText = itemName + " (" + stock.ToString(CultureInfo.InvariantCulture) + ")";

            if (!TargetId.HasValue || !_city.TryGetBuilding(TargetId.Value, out Building? target) || target!.Id == source.Id)
            {
                Path = Array.Empty<(int X, int Y)>();
                WalkSeconds = 0;
                PromptText = "Send from " + sourceName + " — tap a target building";
                DetailText = itemName + " · " + amountLabel + " (" + CarryQuantity.ToString(CultureInfo.InvariantCulture) + ")";
                HintText = string.Empty;
                CanConfirm = false;
                return;
            }

            _finder.Load(_city);
            Path = _finder.Route(source.X, source.Y, target.X, target.Y);
            WalkSeconds = _finder.Measure(source.X, source.Y, target.X, target.Y)
                .Seconds(CityQueries.WalkBonusPermille(_city, _content));
            string targetName = BuildingNameOf(target);

            if (Phase == TripPhase.Sending)
            {
                PromptText = "Sending " + CarryQuantity.ToString(CultureInfo.InvariantCulture) + " " + itemName + " to " + targetName + "…";
                DetailText = "Takes " + DurationText.Format(WalkSeconds) + " · " + amountLabel;
                HintText = string.Empty;
                CanConfirm = false;
                return;
            }

            PromptText = "Send " + CarryQuantity.ToString(CultureInfo.InvariantCulture) + " " + itemName + " to " + targetName;
            DetailText = "Takes " + DurationText.Format(WalkSeconds) + " · " + amountLabel;
            HintText = DisableReasonFor(target);
            CanConfirm = HintText.Length == 0;
        }

        /// <summary>The first reason the Send button is disabled in <see cref="TripRules.StartTrip"/> order, or empty when allowed.</summary>
        private string DisableReasonFor(Building target)
        {
            if (!_availability.IsAllowed)
            {
                return _availability.Reason;
            }

            if (Amount.Mode.HasValue && !CityQueries.IsAmountModeUnlocked(_city!, _content, Amount.Mode.Value))
            {
                return AmountModeLockedReason;
            }

            if (!CityQueries.Accepts(_city!, _content, target, Item))
            {
                return TargetDoesNotAcceptReason;
            }

            if (CityQueries.FreeWorkers(_city!, _content) < 1)
            {
                return NoFreeWorkersReason;
            }

            if (CarryQuantity < 1)
            {
                return NothingToCarryReason;
            }

            return string.Empty;
        }

        /// <summary>What <paramref name="amount"/> gives of <paramref name="baseQuantity"/> before the carry limit (<c>04</c> section 19).</summary>
        private long ResolveAmount(Amount amount, long baseQuantity)
        {
            if (amount.Mode == AmountMode.Exact)
            {
                return amount.Value;
            }

            if (baseQuantity < 1)
            {
                return 0;
            }

            long percent = amount.Mode == AmountMode.Percent
                ? amount.Value
                : (_content.TryGetAmountPreset(amount.PresetId, out AmountPresetDefinition? preset) ? preset!.Percent : 0);
            long share = IntegerMath.FloorDiv(checked(baseQuantity * percent), 100);
            return share < 1 ? 1 : share;
        }

        /// <summary>The display name of <paramref name="building"/> from content.</summary>
        private string BuildingNameOf(Building building)
        {
            return _content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition) ? definition!.Name : building.TypeId.Value;
        }

        /// <summary>Human-readable label for <paramref name="amount"/>.</summary>
        private string FormatAmountLabel(Amount amount)
        {
            if (!amount.Mode.HasValue && _content.TryGetAmountPreset(amount.PresetId, out AmountPresetDefinition? preset))
            {
                return preset!.Name;
            }

            return amount.ToString();
        }
    }
}
