#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Trips;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Trips
{
    /// <summary>
    /// <see cref="TripFlowModel"/> and the <c>UI/TripBar</c> layout (CR-0008): target selection, item cycling, preset and percentage amounts,
    /// <c>PathFinder</c> path and duration, and the Send button's disable reasons in the server's order.
    /// </summary>
    public sealed class TripFlowModelTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>An 8x8 grid matching the content.</summary>
        private CityGrid _grid = null!;

        /// <summary>Before each test: load content and grid.</summary>
        [SetUp]
        public void SetUp()
        {
            _content = HudTestData.Content();
            _grid = new CityGrid(_content.Rules.GridWidth, _content.Rules.GridHeight);
        }

        /// <summary>Builds a city with a stocked sawmill at (0, 0), a warehouse at (3, 0), a cottage at (0, 2), and optional research.</summary>
        private static CityState TestCity(
            long sawmillLogs = 12,
            long sawmillPlanks = 6,
            int freeWorkers = 2,
            string[]? research = null)
        {
            var store = new List<KeyValuePair<ItemId, long>>();
            if (sawmillLogs > 0)
            {
                store.Add(new KeyValuePair<ItemId, long>(new ItemId("logs"), sawmillLogs));
            }

            if (sawmillPlanks > 0)
            {
                store.Add(new KeyValuePair<ItemId, long>(new ItemId("planks"), sawmillPlanks));
            }

            int assignedInSawmill = Math.Max(0, 4 - freeWorkers);
            var sawmill = new Building(1, new BuildingTypeId("sawmill"), 0, 0, 1, assignedInSawmill, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, store);
            var warehouse = new Building(2, new BuildingTypeId("warehouse"), 3, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var cottage = new Building(3, new BuildingTypeId("house"), 0, 2, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);

            var completed = new List<ResearchId>();
            if (research != null)
            {
                foreach (string id in research)
                {
                    completed.Add(new ResearchId(id));
                }
            }

            return new CityState(100, 1, 10, 500, new[] { sawmill, warehouse, cottage }, completed, null);
        }

        /// <summary>Starting from a stocked building enters PickingTarget, selects its first stocked good and All, and caps by tripCarryLimit.</summary>
        [Test]
        public void StartFrom_StockedBuilding_EntersPickingTarget()
        {
            var model = new TripFlowModel(_content, _grid);
            model.Update(TestCity(sawmillLogs: 12, sawmillPlanks: 6));

            Assert.That(model.StartFrom(1), Is.True);
            Assert.That(model.Phase, Is.EqualTo(TripPhase.PickingTarget));
            Assert.That(model.SourceId, Is.EqualTo(1));
            Assert.That(model.Item, Is.EqualTo(new ItemId("logs")));
            Assert.That(model.Amount, Is.EqualTo(Amount.Preset(new AmountPresetId("all"))));
            Assert.That(model.CarryQuantity, Is.EqualTo(10), "capped by tripCarryLimit (10 in mvp-content)");
            Assert.That(model.PromptText, Is.EqualTo("Send from Sawmill — tap a target building"));
            Assert.That(model.CanConfirm, Is.False);
        }

        /// <summary>Selecting a valid target building computes the PathFinder route and walk duration and enables Confirm.</summary>
        [Test]
        public void SelectTile_ValidTarget_ComputesPathAndEnablesConfirm()
        {
            var model = new TripFlowModel(_content, _grid);
            model.Update(TestCity(sawmillLogs: 3, sawmillPlanks: 6));
            model.StartFrom(1);

            model.SelectTile(new GridCoordinate(3, 0));

            Assert.That(model.Phase, Is.EqualTo(TripPhase.Confirming));
            Assert.That(model.TargetId, Is.EqualTo(2));
            Assert.That(model.Path.Count, Is.EqualTo(4), "tiles (0,0), (1,0), (2,0), (3,0)");
            Assert.That(model.WalkSeconds, Is.GreaterThan(0));
            Assert.That(model.PromptText, Is.EqualTo("Send 3 Logs to Warehouse"));
            Assert.That(model.DetailText, Does.StartWith("Takes "));
            Assert.That(model.CanConfirm, Is.True);
            Assert.That(model.HintText, Is.Empty);
        }

        /// <summary>Presets (all, half, quarter), item cycling, and percentage amounts (once researched) update the carry quantity.</summary>
        [Test]
        public void ItemCycling_Presets_AndPercentages_UpdateCarryQuantity()
        {
            var model = new TripFlowModel(_content, _grid);
            model.Update(TestCity(sawmillLogs: 3, sawmillPlanks: 4, research: new[] { "sturdy_boots", "tally_sticks" }));
            model.StartFrom(1);
            model.SelectTile(new GridCoordinate(3, 0));

            Assert.That(model.IsPercentUnlocked, Is.True);
            model.SetPreset("half");
            Assert.That(model.CarryQuantity, Is.EqualTo(1), "half of 3 rounded down is 1");

            model.CycleItem();
            Assert.That(model.Item, Is.EqualTo(new ItemId("planks")));
            Assert.That(model.CarryQuantity, Is.EqualTo(2), "half of 4 is 2");

            model.CyclePercent();
            Assert.That(model.Amount, Is.EqualTo(Amount.Percent(50)));
            Assert.That(model.CarryQuantity, Is.EqualTo(2));

            model.CyclePercent();
            Assert.That(model.Amount, Is.EqualTo(Amount.Percent(75)));
            Assert.That(model.CarryQuantity, Is.EqualTo(3));
        }

        /// <summary>Disable reasons follow the server's order: connection availability, target acceptance, free workers, nothing to carry.</summary>
        [Test]
        public void DisableReasons_FollowServerOrder()
        {
            var model = new TripFlowModel(_content, _grid);
            model.Update(TestCity(sawmillLogs: 0, sawmillPlanks: 4, freeWorkers: 0));
            model.StartFrom(1);

            // Target is the cottage at (0, 2), which neither needs nor stores planks.
            model.SelectTile(new GridCoordinate(0, 2));
            Assert.That(model.CanConfirm, Is.False);
            Assert.That(model.HintText, Is.EqualTo(TripFlowModel.TargetDoesNotAcceptReason));

            // Offline overrides city reasons.
            model.SetCommandAvailability(CommandAvailability.For(ConnectionState.Offline));
            Assert.That(model.HintText, Is.EqualTo(CommandAvailability.OfflineReason));
            model.SetCommandAvailability(CommandAvailability.Allowed);

            // Target is the warehouse at (3, 0), which stores planks, but freeWorkers is 0.
            model.SelectTile(new GridCoordinate(3, 0));
            Assert.That(model.CanConfirm, Is.False);
            Assert.That(model.HintText, Is.EqualTo(TripFlowModel.NoFreeWorkersReason));

            // With a free worker, switch to logs (0 stock -> Nothing to carry).
            model.Update(TestCity(sawmillLogs: 0, sawmillPlanks: 4, freeWorkers: 2));
            model.SetItem(new ItemId("logs"));
            Assert.That(model.CanConfirm, Is.False);
            Assert.That(model.HintText, Is.EqualTo(TripFlowModel.NothingToCarryReason));
        }

        /// <summary>TryConfirm produces a StartTrip intent and enters Sending; FinishCommand closes on success and returns to Confirming on failure.</summary>
        [Test]
        public void TryConfirm_AndFinishCommand_FollowTheLifecycle()
        {
            var model = new TripFlowModel(_content, _grid);
            model.Update(TestCity(sawmillLogs: 4, sawmillPlanks: 0));
            model.StartFrom(1);
            model.SelectTile(new GridCoordinate(3, 0));

            Assert.That(model.TryConfirm(out CommandIntent? intent), Is.True);
            Assert.That(model.Phase, Is.EqualTo(TripPhase.Sending));
            Assert.That(intent, Is.EqualTo(CommandIntent.StartTrip(1, 2, "logs", "all")));

            model.FinishCommand(false);
            Assert.That(model.Phase, Is.EqualTo(TripPhase.Confirming));

            Assert.That(model.TryConfirm(out _), Is.True);
            model.FinishCommand(true);
            Assert.That(model.Phase, Is.EqualTo(TripPhase.Idle));
            Assert.That(model.Path, Is.Empty);
        }

        /// <summary>The TripBar layout has every element the component looks up and its root ignores pointers.</summary>
        [Test]
        public void Layout_HasEveryElementAndRootIgnoresPointer()
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/TripBar");
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/UI/TripBar.uxml is missing");
            VisualElement root = asset.Instantiate();

            Assert.That(root.Q<VisualElement>(TripBar.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(root.Q<VisualElement>(TripBar.BarName).ClassListContains("trip-bar--hidden"), Is.True);
            foreach (string label in new[] { TripBar.PromptName, TripBar.DetailName, TripBar.HintName })
            {
                Assert.That(root.Q<Label>(label), Is.Not.Null, label);
            }

            foreach (string button in new[]
            {
                TripBar.ItemButtonName, TripBar.AllButtonName, TripBar.HalfButtonName, TripBar.QuarterButtonName,
                TripBar.PercentButtonName, TripBar.ConfirmButtonName, TripBar.CancelButtonName,
            })
            {
                Assert.That(root.Q<Button>(button), Is.Not.Null, button);
            }
        }
    }
}
