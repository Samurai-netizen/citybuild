#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Inspector
{
    /// <summary>
    /// What the building inspector (M0-51) says about one building, as plain text and flags: name and level, the status in words and the time to
    /// the next event, the recipe with its rates at the current staffing, the worker stepper with the rate at the pending value, and the upgrade
    /// section with its cost, time, gain and the reason it is disabled. Everything comes from the content, the displayed city and the commands
    /// still on their way (<see cref="InspectorPending"/>, read from the sender's pending list): the pending worker target is the stepper's value, so the model keeps no state of
    /// its own. The rates are the engine's <see cref="ProductionDisplay.PerHour"/> and the status the engine's <see cref="StallReason"/>, so
    /// the screen equals the build card and the simulator. The disabled states are hints only (<c>09</c> UX rules): they follow the server's own
    /// checks in the server's order, but the server decides every command again. It is immutable and two models that say the same compare
    /// equal, so the screen redraws only when something differs. No Unity objects: EditMode tests check every sentence.
    /// </summary>
    public sealed class BuildingInspectorModel : IEquatable<BuildingInspectorModel>
    {
        /// <summary>Creates an empty model; <see cref="Create"/> fills it.</summary>
        private BuildingInspectorModel()
        {
        }

        /// <summary>The building's id in the city: what a stepper or upgrade command names.</summary>
        public int BuildingId { get; private set; }

        /// <summary>The building's type: the screen finds the picture of the type by it.</summary>
        public BuildingTypeId TypeId { get; private set; }

        /// <summary>The type's name from the content, such as <c>Sawmill</c>.</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>The level, such as <c>Level 1 of 2</c>.</summary>
        public string LevelText { get; private set; } = string.Empty;

        /// <summary>What the building is doing or waiting for, such as <c>Producing</c> or <c>Waiting for Logs</c>.</summary>
        public string StatusText { get; private set; } = string.Empty;

        /// <summary>The time to the next event, such as <c>Next batch in 12s</c>; empty when nothing is scheduled (idle, or a frozen cycle).</summary>
        public string TimeText { get; private set; } = string.Empty;

        /// <summary>What the building makes, such as <c>2 Logs → 1 Planks</c>, or what a house provides; empty for a type with neither.</summary>
        public string RecipeText { get; private set; } = string.Empty;

        /// <summary>The per-hour rates at the current staffing, such as <c>Uses 120 Logs/h · Makes 60 Planks/h</c>; empty for a house or while it is built.</summary>
        public string RatesText { get; private set; } = string.Empty;

        /// <summary>Whether the worker stepper is shown: a producer that is built (a house has no workers to set, a site cannot take any yet).</summary>
        public bool HasStepper { get; private set; }

        /// <summary>The stepper's value, such as <c>3 / 3</c>: the pending target (or the assigned workers) over the level's most workers.</summary>
        public string WorkersText { get; private set; } = string.Empty;

        /// <summary>The worker count the stepper shows: the target of the command on its way, or the assigned workers.</summary>
        public int WorkersTarget { get; private set; }

        /// <summary>Whether the − button works: the target is above 0.</summary>
        public bool MinusEnabled { get; private set; }

        /// <summary>Whether the + button works: the target is below the level's most workers and the city has a free worker for one more.</summary>
        public bool PlusEnabled { get; private set; }

        /// <summary>The rates at the pending value, such as <c>With 2 workers: Uses 80 Logs/h · Makes 40 Planks/h</c>; empty when no change is pending.</summary>
        public string PreviewText { get; private set; } = string.Empty;

        /// <summary>The note under the stepper: <c>Updating workers…</c> while a target is on its way, else why + is off; empty otherwise.</summary>
        public string StepperNote { get; private set; } = string.Empty;

        /// <summary>Whether a worker target is on its way to the server.</summary>
        public bool IsWorkersPending { get; private set; }

        /// <summary>The upgrade section's title, such as <c>Upgrade to level 2</c>, or <c>Highest level reached</c>.</summary>
        public string UpgradeTitle { get; private set; } = string.Empty;

        /// <summary>The upgrade's price, such as <c>Cost: 250 Coins, 30 Planks</c>; empty at the highest level.</summary>
        public string UpgradeCostText { get; private set; } = string.Empty;

        /// <summary>The upgrade's duration, such as <c>Takes 3m</c>; empty at the highest level.</summary>
        public string UpgradeTimeText { get; private set; } = string.Empty;

        /// <summary>What the upgrade gives, such as <c>Max workers 3 → 6</c>; empty at the highest level.</summary>
        public string UpgradeGainText { get; private set; } = string.Empty;

        /// <summary>Whether the upgrade button works.</summary>
        public bool CanUpgrade { get; private set; }

        /// <summary>Why the upgrade button is off, such as <c>Not enough Planks</c>; empty while <see cref="CanUpgrade"/> is true.</summary>
        public string UpgradeNote { get; private set; } = string.Empty;

        /// <summary>Whether an upgrade of this building is on its way to the server.</summary>
        public bool IsUpgradePending { get; private set; }

        /// <summary>
        /// Makes the inspector's words for <paramref name="building"/>. The displayed city's clock is the clock to show, as in
        /// <c>BuildingStatusModel</c>, and a command of the building in <paramref name="pending"/> is its pending state.
        /// </summary>
        /// <param name="content">The game content the names and numbers come from.</param>
        /// <param name="city">The displayed city.</param>
        /// <param name="building">A building of <paramref name="city"/>.</param>
        /// <param name="pending">What is on its way for this building (<see cref="InspectorPending.From"/>).</param>
        /// <param name="availability">Whether a command may be sent now (<see cref="CommandAvailability.For"/>): when it may not, − + and Upgrade are off and their notes give the reason (M0-55).</param>
        /// <returns>The model, or null when the content does not know the building's type or level (the screen then shows nothing, never an exception).</returns>
        /// <exception cref="ArgumentNullException">The content, the city or the building is null.</exception>
        public static BuildingInspectorModel? Create(
            GameContent content, CityState city, Building building, InspectorPending pending, CommandAvailability availability)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (building == null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            if (!content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition) || building.Level > definition.MaxLevel)
            {
                return null;
            }

            BuildingLevelDefinition level = definition.GetLevel(building.Level);
            int? pendingTarget = pending.Workers;
            bool upgradePending = pending.Upgrade;
            var model = new BuildingInspectorModel
            {
                BuildingId = building.Id,
                TypeId = building.TypeId,
                Name = definition.Name,
                LevelText = "Level " + Number(building.Level) + " of " + Number(definition.MaxLevel),
                StatusText = StatusTextFor(content, city, building),
                TimeText = TimeTextFor(city, building),
                RecipeText = RecipeTextFor(content, level),
                IsWorkersPending = pendingTarget.HasValue,
                IsUpgradePending = upgradePending,
            };
            model.FillStepper(content, city, building, level, pendingTarget, availability);
            model.FillUpgrade(content, city, building, definition, upgradePending, availability);
            return model;
        }

        /// <summary>True when every text and flag is the same: the screen has nothing to redraw.</summary>
        public bool Equals(BuildingInspectorModel? other)
        {
            return other != null
                && BuildingId == other.BuildingId
                && TypeId.Equals(other.TypeId)
                && Name == other.Name
                && LevelText == other.LevelText
                && StatusText == other.StatusText
                && TimeText == other.TimeText
                && RecipeText == other.RecipeText
                && RatesText == other.RatesText
                && HasStepper == other.HasStepper
                && WorkersText == other.WorkersText
                && WorkersTarget == other.WorkersTarget
                && MinusEnabled == other.MinusEnabled
                && PlusEnabled == other.PlusEnabled
                && PreviewText == other.PreviewText
                && StepperNote == other.StepperNote
                && IsWorkersPending == other.IsWorkersPending
                && UpgradeTitle == other.UpgradeTitle
                && UpgradeCostText == other.UpgradeCostText
                && UpgradeTimeText == other.UpgradeTimeText
                && UpgradeGainText == other.UpgradeGainText
                && CanUpgrade == other.CanUpgrade
                && UpgradeNote == other.UpgradeNote
                && IsUpgradePending == other.IsUpgradePending;
        }

        /// <summary>See <see cref="Equals(BuildingInspectorModel?)"/>.</summary>
        public override bool Equals(object? obj) => Equals(obj as BuildingInspectorModel);

        /// <summary>A hash of the building and the texts that change most, which goes with <see cref="Equals(BuildingInspectorModel?)"/>.</summary>
        public override int GetHashCode() => HashCode.Combine(BuildingId, StatusText, TimeText, WorkersText, UpgradeNote);

        /// <summary>
        /// Sets the stepper's members. A producer that is built has a stepper; its rates are shown at the assigned workers, and, when a
        /// target is on its way, at that target too. While commands are not allowed both buttons are off and the note is the reason; the shown
        /// value, the pending target included, stays, because the sender is still on its way with it.
        /// </summary>
        private void FillStepper(
            GameContent content,
            CityState city,
            Building building,
            BuildingLevelDefinition level,
            int? pendingTarget,
            CommandAvailability availability)
        {
            HasStepper = level.Recipe != null && building.Phase != BuildingPhase.Constructing;
            if (level.Recipe == null || building.Phase == BuildingPhase.Constructing)
            {
                return;
            }

            int assigned = Math.Min(building.AssignedWorkers, level.MaxWorkers);
            int target = Math.Max(0, Math.Min(pendingTarget ?? assigned, level.MaxWorkers));
            long free = CityQueries.FreeWorkers(city, content);
            WorkersTarget = target;
            WorkersText = Number(target) + " / " + Number(level.MaxWorkers);
            RatesText = RatesTextAt(content, city, building, assigned);
            MinusEnabled = availability.IsAllowed && target > 0;
            // One more worker than now must fit the free ones: a target already above the assigned count has used some of them.
            bool plusFits = target < level.MaxWorkers && (long)target + 1 - assigned <= free;
            PlusEnabled = availability.IsAllowed && plusFits;
            if (target != assigned)
            {
                PreviewText = "With " + Number(target) + (target == 1 ? " worker: " : " workers: ") + RatesTextAt(content, city, building, target);
            }

            if (!availability.IsAllowed)
            {
                StepperNote = availability.Reason;
            }
            else if (pendingTarget.HasValue)
            {
                StepperNote = "Updating workers…";
            }
            else if (target >= level.MaxWorkers)
            {
                StepperNote = "At most " + Number(level.MaxWorkers) + " workers at this level";
            }
            else if (!plusFits)
            {
                StepperNote = "No free workers";
            }
        }

        /// <summary>
        /// Sets the upgrade section's members. The disabled reason follows the server's checks in the server's order (busy, highest level, no
        /// free builder, not enough items), with the upgrade on its way first. While commands are not allowed the reason comes before all of them.
        /// </summary>
        private void FillUpgrade(
            GameContent content,
            CityState city,
            Building building,
            BuildingDefinition definition,
            bool upgradePending,
            CommandAvailability availability)
        {
            if (building.Level >= definition.MaxLevel)
            {
                UpgradeTitle = "Highest level reached";
                UpgradeNote = upgradePending ? "Upgrade requested…" : string.Empty;
                return;
            }

            BuildingLevelDefinition current = definition.GetLevel(building.Level);
            BuildingLevelDefinition next = definition.GetLevel(building.Level + 1);
            UpgradeTitle = "Upgrade to level " + Number(next.Level);
            UpgradeCostText = "Cost: " + ItemText.Quantities(content, next.UpgradeCost, ", ");
            UpgradeTimeText = "Takes " + DurationText.Format(next.UpgradeSeconds);
            UpgradeGainText = current.Recipe != null
                ? "Max workers " + Number(current.MaxWorkers) + " → " + Number(next.MaxWorkers)
                : "Provides " + Number(current.WorkersProvided) + " → " + Number(next.WorkersProvided) + " workers";
            UpgradeNote = availability.IsAllowed ? UpgradeNoteFor(content, city, building, next, upgradePending) : availability.Reason;
            CanUpgrade = UpgradeNote.Length == 0;
        }

        /// <summary>The first reason the upgrade cannot start, or empty when nothing the client knows stands in the way.</summary>
        private static string UpgradeNoteFor(
            GameContent content, CityState city, Building building, BuildingLevelDefinition next, bool upgradePending)
        {
            if (upgradePending)
            {
                return "Upgrade requested…";
            }

            if (building.Phase == BuildingPhase.Constructing || building.Phase == BuildingPhase.Upgrading)
            {
                return "Busy until the current job is done";
            }

            if (CityQueries.BuildersBusy(city) >= content.Rules.BuilderSlots)
            {
                return "All builders are busy";
            }

            string missing = string.Empty;
            for (int index = 0; index < next.UpgradeCost.Count; index++)
            {
                ItemQuantity line = next.UpgradeCost[index];
                if (city.GetQuantity(line.Item) < line.Quantity)
                {
                    missing += (missing.Length == 0 ? "Not enough " : ", ") + ItemText.ItemName(content, line.Item);
                }
            }

            return missing;
        }

        /// <summary>The status in words: the job in progress, or the engine's stall reason when nothing is.</summary>
        private static string StatusTextFor(GameContent content, CityState city, Building building)
        {
            switch (building.Phase)
            {
                case BuildingPhase.Constructing:
                    return "Under construction";
                case BuildingPhase.Upgrading:
                    return "Upgrading to level " + Number(building.TargetLevel);
            }

            StallReason? reason = StallReason.Of(city, content, building);
            if (reason == null)
            {
                return building.Phase == BuildingPhase.Producing ? "Producing" : "Ready";
            }

            switch (reason.Kind)
            {
                case StallKind.NoWorkers:
                    return "No workers assigned";
                case StallKind.MissingInput:
                    return "Waiting for " + ItemText.ItemName(content, reason.Item);
                case StallKind.StorageFull:
                    return ItemText.ItemName(content, reason.Item) + " storage is full";
                default:
                    return "Ready";
            }
        }

        /// <summary>The time to the end of the job at the displayed clock, worded for the phase; empty when no end is scheduled.</summary>
        private static string TimeTextFor(CityState city, Building building)
        {
            long? seconds = ProgressDisplay.SecondsUntilNextEvent(building, city.Clock);
            if (!seconds.HasValue)
            {
                return string.Empty;
            }

            string time = DurationText.Format(seconds.Value);
            switch (building.Phase)
            {
                case BuildingPhase.Constructing:
                    return "Ready in " + time;
                case BuildingPhase.Upgrading:
                    return "Upgrade done in " + time;
                default:
                    return "Next batch in " + time;
            }
        }

        /// <summary>The recipe of a level, <c>2 Logs → 1 Planks</c>, or what a house provides; empty when it has neither.</summary>
        private static string RecipeTextFor(GameContent content, BuildingLevelDefinition level)
        {
            RecipeDefinition? recipe = level.Recipe;
            if (recipe == null)
            {
                return level.WorkersProvided > 0 ? "Provides " + Number(level.WorkersProvided) + " workers" : string.Empty;
            }

            string inputs = recipe.Inputs.Count == 0 ? "Nothing" : ItemText.Quantities(content, recipe.Inputs, " + ");
            return inputs + " → " + ItemText.Quantities(content, recipe.Outputs, " + ");
        }

        /// <summary>
        /// The rates of the building's level at <paramref name="workers"/> workers, <c>Uses 120 Logs/h · Makes 60 Planks/h</c> (no "Uses" part
        /// for a recipe with no inputs). The engine's <see cref="ProductionDisplay.PerHour"/> does the maths, research included.
        /// </summary>
        private static string RatesTextAt(GameContent content, CityState city, Building building, int workers)
        {
            ProductionRates rates = ProductionDisplay.PerHour(city, content, building, workers);
            string makes = "Makes " + ItemText.Rates(content, rates.Outputs);
            return rates.Inputs.Count == 0 ? makes : "Uses " + ItemText.Rates(content, rates.Inputs) + " · " + makes;
        }

        /// <summary>A whole number in text, never in the machine's culture.</summary>
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
