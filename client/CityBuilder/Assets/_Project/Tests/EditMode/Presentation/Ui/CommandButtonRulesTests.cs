#nullable enable

using System;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Inspector;
using CityBuilder.Client.Presentation.Research;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Tests.EditMode.Presentation.Build;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Ui
{
    /// <summary>
    /// The enable rule of every command button in every connection state (M0-55): the stepper's − and +, Upgrade, Finance and Place work when
    /// the client is connected and the city allows them, and in every other state they are off with the connection's reason, whatever the city
    /// says. What is on its way (a pending worker target, a pending research) stays shown, because the sender is still sending it.
    /// </summary>
    public sealed class CommandButtonRulesTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>
        /// The inspector of a sawmill with one of its three workers, next to a house with room for more, in a city that can pay the upgrade:
        /// every button works when the connection allows it.
        /// </summary>
        private BuildingInspectorModel Inspector(CommandAvailability availability, InspectorPending pending = default)
        {
            Building[] buildings = { HudTestData.Producer(1, 0, "sawmill", 1, 180), HudTestData.House(2, 1) };
            CityState city = HudTestData.City(0, 700, 40, 30, buildings);
            Assert.That(city.TryGetBuilding(1, out Building? sawmill), Is.True);
            BuildingInspectorModel? model = BuildingInspectorModel.Create(_content, city, sawmill!, pending, availability);
            Assert.That(model, Is.Not.Null);
            return model!;
        }

        /// <summary>The model of a research node the city can pay and has no other research running: its button works when the connection allows it.</summary>
        private ResearchNodeModel Finance(CommandAvailability availability, string? pending = null)
        {
            CityState city = HudTestData.City(1000, 250, 0, 0, new Building[0]);
            Assert.That(_content.TryGetResearch(new ResearchId("sharp_axes"), out ResearchDefinition? definition), Is.True);
            return ResearchNodeModel.Create(_content, city, definition!, 0, pending, availability);
        }

        /// <summary>A placement flow with the sawmill and a free tile picked, so only the connection stands between the player and Place.</summary>
        private static PlacementFlowModel Placement()
        {
            var flow = new PlacementFlowModel(BuildMenuTestData.Content(), new CityGrid(8, 8));
            flow.Update(BuildMenuTestData.StartCity());
            flow.SetType(new BuildingTypeId("sawmill"));
            flow.SetTile(new GridCoordinate(3, 4));
            return flow;
        }

        /// <summary>Every state but Connected, which the cases below must switch off.</summary>
        private static readonly ConnectionState[] NotConnected =
        {
            ConnectionState.Connecting,
            ConnectionState.Offline,
            ConnectionState.ServerError,
            ConnectionState.Unauthorized,
            ConnectionState.IncompatibleVersion,
        };

        /// <summary>The control case: connected, − + and Upgrade all work and have no note.</summary>
        [Test]
        public void Inspector_Connected_EveryButtonWorks()
        {
            BuildingInspectorModel model = Inspector(CommandAvailability.For(ConnectionState.Connected));

            Assert.That(model.MinusEnabled, Is.True);
            Assert.That(model.PlusEnabled, Is.True);
            Assert.That(model.CanUpgrade, Is.True);
            Assert.That(model.StepperNote, Is.Empty);
            Assert.That(model.UpgradeNote, Is.Empty);
        }

        /// <summary>In every other state − + and Upgrade are off, each section's note is the reason, and the shown values are the city's.</summary>
        [TestCaseSource(nameof(NotConnected))]
        public void Inspector_NotConnected_EveryButtonIsOff_WithTheReason(ConnectionState state)
        {
            CommandAvailability availability = CommandAvailability.For(state);

            BuildingInspectorModel model = Inspector(availability);

            Assert.That(model.MinusEnabled, Is.False);
            Assert.That(model.PlusEnabled, Is.False);
            Assert.That(model.CanUpgrade, Is.False);
            Assert.That(model.StepperNote, Is.EqualTo(availability.Reason));
            Assert.That(model.UpgradeNote, Is.EqualTo(availability.Reason));
            Assert.That(model.WorkersText, Is.EqualTo("1 / 3"));
            Assert.That(model.UpgradeTitle, Is.EqualTo("Upgrade to level 2"));
        }

        /// <summary>A worker target on its way stays the stepper's value while offline, so the player sees what the sender is still sending.</summary>
        [Test]
        public void Inspector_Offline_KeepsThePendingTargetVisible()
        {
            BuildingInspectorModel model = Inspector(CommandAvailability.For(ConnectionState.Offline), new InspectorPending(2, false));

            Assert.That(model.IsWorkersPending, Is.True);
            Assert.That(model.WorkersTarget, Is.EqualTo(2));
            Assert.That(model.WorkersText, Is.EqualTo("2 / 3"));
            Assert.That(model.PreviewText, Is.EqualTo("With 2 workers: Uses 80 Logs/h · Makes 40 Planks/h"));
            Assert.That(model.MinusEnabled, Is.False);
            Assert.That(model.PlusEnabled, Is.False);
            Assert.That(model.StepperNote, Is.EqualTo(CommandAvailability.OfflineReason));
        }

        /// <summary>An upgrade on its way is still marked as requested while offline.</summary>
        [Test]
        public void Inspector_Offline_KeepsThePendingUpgradeVisible()
        {
            BuildingInspectorModel model = Inspector(CommandAvailability.For(ConnectionState.Offline), new InspectorPending(null, true));

            Assert.That(model.IsUpgradePending, Is.True);
            Assert.That(model.CanUpgrade, Is.False);
        }

        /// <summary>When the connection is back the same city gives the same buttons as before: nothing is remembered about the outage.</summary>
        [Test]
        public void Inspector_BackOnline_MatchesTheNeverOfflineModel()
        {
            BuildingInspectorModel before = Inspector(CommandAvailability.For(ConnectionState.Connected));
            Inspector(CommandAvailability.For(ConnectionState.Offline));
            BuildingInspectorModel after = Inspector(CommandAvailability.For(ConnectionState.Connected));

            Assert.That(after, Is.EqualTo(before));
        }

        /// <summary>The control case: connected, the Finance button of an affordable node works.</summary>
        [Test]
        public void Research_Connected_FinanceWorks()
        {
            ResearchNodeModel node = Finance(CommandAvailability.For(ConnectionState.Connected));

            Assert.That(node.ShowsButton, Is.True);
            Assert.That(node.ButtonEnabled, Is.True);
            Assert.That(node.ButtonNote, Is.Empty);
        }

        /// <summary>In every other state Finance is off with the reason, even for a node the city could pay.</summary>
        [TestCaseSource(nameof(NotConnected))]
        public void Research_NotConnected_FinanceIsOff_WithTheReason(ConnectionState state)
        {
            CommandAvailability availability = CommandAvailability.For(state);

            ResearchNodeModel node = Finance(availability);

            Assert.That(node.ShowsButton, Is.True);
            Assert.That(node.ButtonEnabled, Is.False);
            Assert.That(node.ButtonNote, Is.EqualTo(availability.Reason));
            Assert.That(node.StatusText, Is.EqualTo("Ready to research"));
        }

        /// <summary>Every Finance button of the screen is off while offline, and the screen still lists every node of the era.</summary>
        [Test]
        public void ResearchScreen_Offline_NoFinanceButtonWorks()
        {
            CityState city = HudTestData.City(1000, 5000, 500, 500, new Building[0]);
            CommandAvailability offline = CommandAvailability.For(ConnectionState.Offline);

            ResearchScreenModel connected = ResearchScreenModel.Create(_content, city, null, CommandAvailability.Allowed);
            ResearchScreenModel model = ResearchScreenModel.Create(_content, city, null, offline);

            Assert.That(model.Nodes.Count, Is.EqualTo(connected.Nodes.Count));
            Assert.That(connected.Nodes, Has.Some.Matches<ResearchNodeModel>(node => node.ButtonEnabled));
            Assert.That(model.Nodes, Has.None.Matches<ResearchNodeModel>(node => node.ButtonEnabled));
        }

        /// <summary>A research on its way is still listed while offline; its button says the connection's reason, not the "starting" note that only a live connection can end.</summary>
        [Test]
        public void Research_Offline_WithAPendingStart_StillShowsTheNode()
        {
            ResearchNodeModel node = Finance(CommandAvailability.For(ConnectionState.Offline), "sharp_axes");

            Assert.That(node.ButtonEnabled, Is.False);
            Assert.That(node.ButtonNote, Is.EqualTo(CommandAvailability.OfflineReason));
            Assert.That(node.Name, Is.EqualTo("Sharpened Axes"));
        }

        /// <summary>The control case: with the sawmill and a free tile picked and a connection, Place works and there is no hint.</summary>
        [Test]
        public void Placement_Connected_PlaceWorks()
        {
            PlacementFlowModel flow = Placement();

            Assert.That(flow.CanConfirm, Is.True);
            Assert.That(flow.HintText, Is.Empty);
        }

        /// <summary>In every other state Place is off, the hint is the reason, nothing is confirmed, and the card and tile stay picked.</summary>
        [TestCaseSource(nameof(NotConnected))]
        public void Placement_NotConnected_PlaceIsOff_WithTheReason(ConnectionState state)
        {
            PlacementFlowModel flow = Placement();
            CommandAvailability availability = CommandAvailability.For(state);

            flow.SetCommandAvailability(availability);

            Assert.That(flow.CanConfirm, Is.False);
            Assert.That(flow.HintText, Is.EqualTo(availability.Reason));
            Assert.That(flow.TryConfirm(out CommandIntent? intent), Is.False);
            Assert.That(intent, Is.Null);
            Assert.That(flow.Phase, Is.EqualTo(PlacementPhase.Confirming));
            Assert.That(flow.TypeId, Is.EqualTo(new BuildingTypeId("sawmill")));
            Assert.That(flow.Tile, Is.EqualTo(new GridCoordinate(3, 4)));
        }

        /// <summary>The player keeps their pick through an outage: when the connection returns Place works again with no new pick.</summary>
        [Test]
        public void Placement_BackOnline_PlaceWorksAgain()
        {
            PlacementFlowModel flow = Placement();
            flow.SetCommandAvailability(CommandAvailability.For(ConnectionState.Offline));

            flow.SetCommandAvailability(CommandAvailability.For(ConnectionState.Connected));

            Assert.That(flow.CanConfirm, Is.True);
            Assert.That(flow.HintText, Is.Empty);
        }

        /// <summary>A change of availability redraws the bar once, and the same availability again redraws nothing.</summary>
        [Test]
        public void Placement_AvailabilityChange_RaisesChangedOnlyWhenItDiffers()
        {
            PlacementFlowModel flow = Placement();
            int changed = 0;
            flow.Changed += () => changed++;

            flow.SetCommandAvailability(CommandAvailability.For(ConnectionState.Offline));
            flow.SetCommandAvailability(CommandAvailability.For(ConnectionState.Offline));
            flow.SetCommandAvailability(CommandAvailability.For(ConnectionState.Connected));

            Assert.That(changed, Is.EqualTo(2));
        }

        /// <summary>A command already on its way is not taken back by an outage: the phase stays Sending.</summary>
        [Test]
        public void Placement_Offline_WhileSending_KeepsTheCommandOnItsWay()
        {
            PlacementFlowModel flow = Placement();
            Assert.That(flow.TryConfirm(out _), Is.True);

            flow.SetCommandAvailability(CommandAvailability.For(ConnectionState.Offline));

            Assert.That(flow.Phase, Is.EqualTo(PlacementPhase.Sending));
            Assert.That(flow.PromptText, Does.StartWith("Building "));
        }

        /// <summary>With no tile picked yet there is no Place button to explain: the hint stays empty and the prompt is the usual one.</summary>
        [Test]
        public void Placement_Offline_BeforeATileIsPicked_HasNoHint()
        {
            var flow = new PlacementFlowModel(BuildMenuTestData.Content(), new CityGrid(8, 8));
            flow.SetType(new BuildingTypeId("sawmill"));

            flow.SetCommandAvailability(CommandAvailability.For(ConnectionState.Offline));

            Assert.That(flow.HintText, Is.Empty);
            Assert.That(flow.PromptText, Does.EndWith("tap a tile to build on"));
        }
    }
}
