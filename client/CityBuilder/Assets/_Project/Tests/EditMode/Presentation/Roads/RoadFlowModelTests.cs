#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Roads;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Roads
{
    /// <summary>
    /// <see cref="RoadFlowModel"/> and the <c>UI/RoadBar</c> layout (CR-0008): road build mode from the build menu, line selection, total coin cost,
    /// per-tile refusal reasons, and upgrading or removing existing road tiles.
    /// </summary>
    public sealed class RoadFlowModelTests
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

        /// <summary>Builds a city with a cottage at (0, 0), optional roads, and the given purse.</summary>
        private static CityState TestCity(long purse = 100, IReadOnlyList<RoadTile>? roads = null)
        {
            var cottage = new Building(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            return new CityState(
                100,
                1,
                10,
                purse,
                new[] { cottage },
                Array.Empty<ResearchId>(),
                null,
                roads ?? Array.Empty<RoadTile>(),
                Array.Empty<Trip>(),
                1,
                Array.Empty<Courier>(),
                Array.Empty<Route>());
        }

        /// <summary>StartBuildMode enters PickingTiles, shows free/occupied hints, and selecting a line computes total coins and enables Place.</summary>
        [Test]
        public void StartBuildMode_AndSelectLine_ComputesTotalCoins_AndEnablesPlace()
        {
            var model = new RoadFlowModel(_content, _grid);
            model.Update(TestCity(purse: 100));

            model.StartBuildMode();

            Assert.That(model.Phase, Is.EqualTo(RoadPhase.PickingTiles));
            Assert.That(model.ShowsHints, Is.True);
            Assert.That(model.ShowsPlace, Is.True);
            Assert.That(model.ShowsEditExisting, Is.False);
            Assert.That(model.CanPlace, Is.False);
            Assert.That(model.Hints[0], Is.EqualTo(TileHint.Occupied), "cottage at (0,0)");
            Assert.That(model.Hints[1], Is.EqualTo(TileHint.Free));

            model.SelectLine(new GridCoordinate(1, 0), new GridCoordinate(3, 0));

            Assert.That(model.Phase, Is.EqualTo(RoadPhase.Confirming));
            Assert.That(model.Tiles.Count, Is.EqualTo(3));
            Assert.That(model.TotalCoins, Is.EqualTo(15), "3 tiles * 5 coins");
            Assert.That(model.PromptText, Is.EqualTo("Build Dirt path (3 tiles)?"));
            Assert.That(model.DetailText, Does.StartWith("Cost: 15 Coins"));
            Assert.That(model.CanPlace, Is.True);
            Assert.That(model.HintText, Is.Empty);
        }

        /// <summary>Refusal reasons in build mode follow RoadRules.PlaceRoads order: connection availability, out of bounds, occupied tile, insufficient coins.</summary>
        [Test]
        public void PlaceRefusalReasons_FollowServerOrder()
        {
            var model = new RoadFlowModel(_content, _grid);
            model.Update(TestCity(purse: 10, roads: new[] { new RoadTile(2, 0, 1) }));
            model.StartBuildMode();

            // Out of bounds.
            model.SelectLine(new GridCoordinate(7, 0), new GridCoordinate(8, 0));
            Assert.That(model.CanPlace, Is.False);
            Assert.That(model.HintText, Is.EqualTo("Outside the city at (8, 0)"));

            // Occupied by a building at (0, 0).
            model.SelectLine(new GridCoordinate(0, 0), new GridCoordinate(1, 0));
            Assert.That(model.CanPlace, Is.False);
            Assert.That(model.HintText, Is.EqualTo("Tile occupied at (0, 0)"));

            // Occupied by an existing road at (2, 0).
            model.SelectLine(new GridCoordinate(1, 0), new GridCoordinate(2, 0));
            Assert.That(model.CanPlace, Is.False);
            Assert.That(model.HintText, Is.EqualTo("Tile occupied at (2, 0)"));

            // Offline overrides city checks.
            model.SetCommandAvailability(CommandAvailability.For(ConnectionState.Offline));
            Assert.That(model.HintText, Is.EqualTo(CommandAvailability.OfflineReason));
            model.SetCommandAvailability(CommandAvailability.Allowed);

            // Insufficient coins (3 free tiles = 15 coins > purse 10).
            model.SelectLine(new GridCoordinate(1, 1), new GridCoordinate(3, 1));
            Assert.That(model.CanPlace, Is.False);
            Assert.That(model.HintText, Is.EqualTo("Not enough Coins"));
        }

        /// <summary>InspectRoadAt opens edit mode for an existing road tile and allows Upgrade and Remove; level 2 roads disable Upgrade with MaxLevelReason.</summary>
        [Test]
        public void InspectRoadAt_EnablesUpgradeAndRemove_AndChecksMaxLevelAndCoins()
        {
            var model = new RoadFlowModel(_content, _grid);
            model.Update(TestCity(purse: 50, roads: new[] { new RoadTile(1, 0, 1), new RoadTile(2, 0, 2) }));

            Assert.That(model.InspectRoadAt(new GridCoordinate(3, 0)), Is.False, "no road at (3,0)");
            Assert.That(model.InspectRoadAt(new GridCoordinate(1, 0)), Is.True);
            Assert.That(model.Phase, Is.EqualTo(RoadPhase.Confirming));
            Assert.That(model.ShowsEditExisting, Is.True);
            Assert.That(model.ShowsPlace, Is.False);
            Assert.That(model.CanUpgrade, Is.True);
            Assert.That(model.CanRemove, Is.True);
            Assert.That(model.TotalCoins, Is.EqualTo(15));

            Assert.That(model.TryConfirmUpgrade(out CommandIntent? upgradeIntent), Is.True);
            Assert.That(upgradeIntent, Is.EqualTo(CommandIntent.UpgradeRoads(new[] { new RoadTileRequestDto(1, 0) })));
            model.FinishCommand(false);

            Assert.That(model.TryConfirmRemove(out CommandIntent? removeIntent), Is.True);
            Assert.That(removeIntent, Is.EqualTo(CommandIntent.RemoveRoads(new[] { new RoadTileRequestDto(1, 0) })));
            model.FinishCommand(true);
            Assert.That(model.Phase, Is.EqualTo(RoadPhase.Idle));

            // Level 2 road at (2, 0) is already at the highest level, so Upgrade is off and Remove stays on.
            Assert.That(model.InspectRoadAt(new GridCoordinate(2, 0)), Is.True);
            Assert.That(model.CanUpgrade, Is.False);
            Assert.That(model.HintText, Is.EqualTo(RoadFlowModel.MaxLevelReason));
            Assert.That(model.CanRemove, Is.True);
        }

        /// <summary>The RoadBar layout has every element the component looks up and its root ignores pointers.</summary>
        [Test]
        public void Layout_HasEveryElementAndRootIgnoresPointer()
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/RoadBar");
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/UI/RoadBar.uxml is missing");
            VisualElement root = asset.Instantiate();

            Assert.That(root.Q<VisualElement>(RoadBar.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(root.Q<VisualElement>(RoadBar.BarName).ClassListContains("road-bar--hidden"), Is.True);
            foreach (string label in new[] { RoadBar.PromptName, RoadBar.DetailName, RoadBar.HintName })
            {
                Assert.That(root.Q<Label>(label), Is.Not.Null, label);
            }

            foreach (string button in new[] { RoadBar.PlaceButtonName, RoadBar.UpgradeButtonName, RoadBar.RemoveButtonName, RoadBar.CancelButtonName })
            {
                Assert.That(root.Q<Button>(button), Is.Not.Null, button);
            }
        }
    }
}
