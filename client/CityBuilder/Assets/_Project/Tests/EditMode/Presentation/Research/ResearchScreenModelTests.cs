#nullable enable

using System.Linq;
using CityBuilder.Client.Presentation.Research;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Research
{
    /// <summary>The research screen as a whole (M0-52): the era header and rule, the nodes of the era and their columns.</summary>
    public sealed class ResearchScreenModelTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>A city of Era I with 1000 coins and the given research.</summary>
        private static CityState City(string[]? done = null, ActiveResearch? active = null)
        {
            return HudTestData.City(1000, 1000, 100, 100, new Building[0], done, active);
        }

        /// <summary>The header names the era and counts the researched nodes out of all of the era's nodes.</summary>
        [Test]
        public void Header_CountsResearchedNodes()
        {
            Assert.That(ResearchScreenModel.Create(_content, City(), null, CommandAvailability.Allowed).HeaderText, Is.EqualTo("Early Middle Ages — 0 / 6 researched"));
            Assert.That(
                ResearchScreenModel.Create(_content, City(new[] { "sharp_axes" }), null, CommandAvailability.Allowed).HeaderText,
                Is.EqualTo("Early Middle Ages — 1 / 6 researched"));
        }

        /// <summary>A node that is running is not counted as researched.</summary>
        [Test]
        public void Header_DoesNotCountTheRunningNode()
        {
            CityState city = City(new[] { "sharp_axes" }, HudTestData.Research("trade_charter", 900, 1500));

            Assert.That(ResearchScreenModel.Create(_content, city, null, CommandAvailability.Allowed).HeaderText, Is.EqualTo("Early Middle Ages — 1 / 6 researched"));
        }

        /// <summary>The rule is worded as the owner words it, and is greyed out while the content has no later era (every M0 content).</summary>
        [Test]
        public void Rule_IsGreyedOut_WhileThereIsNoNextEra()
        {
            ResearchScreenModel model = ResearchScreenModel.Create(_content, City(), null, CommandAvailability.Allowed);

            Assert.That(ResearchScreenModel.RuleText, Is.EqualTo("Complete all to advance"));
            Assert.That(model.RuleIsActive, Is.False);
        }

        /// <summary>A node with no prerequisite is in column 0, a node that requires one is one column further right, and the nodes are ordered by column.</summary>
        [Test]
        public void Nodes_AreInColumnsByPrerequisites()
        {
            ResearchScreenModel model = ResearchScreenModel.Create(_content, City(), null, CommandAvailability.Allowed);

            Assert.That(model.ColumnCount, Is.EqualTo(2));
            Assert.That(model.Nodes.Count, Is.EqualTo(6));
            Assert.That(
                model.Nodes.Select(node => node.Id),
                Is.EqualTo(new[] { "sharp_axes", "sturdy_boots", "trade_charter", "frame_saw", "storehouses", "tally_sticks" }));
            Assert.That(model.Nodes.Select(node => node.Column), Is.EqualTo(new[] { 0, 0, 0, 1, 1, 1 }));
        }

        /// <summary>The nodes carry the status the city gives them.</summary>
        [Test]
        public void Nodes_CarryTheirStatus()
        {
            CityState city = City(new[] { "sharp_axes" }, HudTestData.Research("trade_charter", 900, 1500));

            ResearchScreenModel model = ResearchScreenModel.Create(_content, city, null, CommandAvailability.Allowed);

            Assert.That(
                model.Nodes.Select(node => node.Id + " " + node.Status),
                Is.EqualTo(new[]
                {
                    "sharp_axes Done", "sturdy_boots Available", "trade_charter InProgress", "frame_saw Available", "storehouses Locked",
                    "tally_sticks Locked",
                }));
        }

        /// <summary>The pending research id reaches every node: with a start on its way no button works.</summary>
        [Test]
        public void PendingStart_ReachesEveryNode()
        {
            ResearchScreenModel model = ResearchScreenModel.Create(_content, City(), "sharp_axes", CommandAvailability.Allowed);

            foreach (ResearchNodeModel node in model.Nodes)
            {
                Assert.That(node.ButtonEnabled, Is.False, node.Id);
            }
        }

        /// <summary>Models that say the same compare equal; a research that starts changes the model.</summary>
        [Test]
        public void Equality_FollowsTheNodes()
        {
            ResearchScreenModel first = ResearchScreenModel.Create(_content, City(), null, CommandAvailability.Allowed);
            ResearchScreenModel same = ResearchScreenModel.Create(_content, City(), null, CommandAvailability.Allowed);
            ResearchScreenModel running = ResearchScreenModel.Create(_content, City(null, HudTestData.Research("sharp_axes", 900, 2700)), null, CommandAvailability.Allowed);

            Assert.That(same, Is.EqualTo(first));
            Assert.That(same.GetHashCode(), Is.EqualTo(first.GetHashCode()));
            Assert.That(running, Is.Not.EqualTo(first));
        }
    }
}
