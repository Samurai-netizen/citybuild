#nullable enable

using CityBuilder.Client.Presentation.Research;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Research
{
    /// <summary>One research node of the screen (M0-52): its words, the status derived from the city, and the Finance button with its reasons.</summary>
    public sealed class ResearchNodeModelTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>Makes the model of the node <paramref name="id"/> for <paramref name="city"/>.</summary>
        private ResearchNodeModel Node(string id, CityState city, string? pending = null, int column = 0)
        {
            Assert.That(_content.TryGetResearch(new ResearchId(id), out ResearchDefinition? definition), Is.True);
            return ResearchNodeModel.Create(_content, city, definition!, column, pending);
        }

        /// <summary>A city of Era I with the given coins and logs, planks, and completed research.</summary>
        private static CityState City(long coins, long logs = 0, long planks = 0, string[]? done = null, ActiveResearch? active = null)
        {
            return HudTestData.City(1000, coins, logs, planks, new Building[0], done, active);
        }

        /// <summary>Name, cost, duration and effect come from the content: an unlock names the building type.</summary>
        [Test]
        public void Words_ComeFromTheContent_ForAnUnlock()
        {
            ResearchNodeModel node = Node("trade_charter", City(500));

            Assert.That(node.Id, Is.EqualTo("trade_charter"));
            Assert.That(node.Name, Is.EqualTo("Trade Charter"));
            Assert.That(node.EffectText, Is.EqualTo("Unlocks Trading Post"));
            Assert.That(node.CostText, Is.EqualTo("Cost: 150 Coins"));
            Assert.That(node.TimeText, Is.EqualTo("Takes 10m"));
        }

        /// <summary>A labor reduction says which building gets how many percent less labor (200 permille is 20 %).</summary>
        [Test]
        public void Words_ForALaborReduction()
        {
            ResearchNodeModel node = Node("sharp_axes", City(500));

            Assert.That(node.EffectText, Is.EqualTo("Woodcutter's Hut: −20% labor"));
            Assert.That(node.TimeText, Is.EqualTo("Takes 30m"));
        }

        /// <summary>A storage bonus lists each item, and a cost with two items lists both.</summary>
        [Test]
        public void Words_ForStorageBonusesAndATwoItemCost()
        {
            ResearchNodeModel node = Node("storehouses", City(500, 100, 0, new[] { "trade_charter" }));

            Assert.That(node.EffectText, Is.EqualTo("+250 Logs storage, +250 Planks storage"));
            Assert.That(node.CostText, Is.EqualTo("Cost: 300 Coins, 50 Logs"));
        }

        /// <summary>A node with no prerequisite and enough stock is available, with a working button and no note.</summary>
        [Test]
        public void Available_WithEnoughStock_CanBeFinanced()
        {
            ResearchNodeModel node = Node("sharp_axes", City(250));

            Assert.That(node.Status, Is.EqualTo(ResearchStatus.Available));
            Assert.That(node.StatusText, Is.EqualTo("Ready to research"));
            Assert.That(node.ShowsButton, Is.True);
            Assert.That(node.ButtonEnabled, Is.True);
            Assert.That(node.ButtonNote, Is.Empty);
        }

        /// <summary>A missing prerequisite makes the node locked, names it, and turns the button off with the same words.</summary>
        [Test]
        public void Locked_NamesTheMissingPrerequisite()
        {
            ResearchNodeModel node = Node("frame_saw", City(1000, 0, 100));

            Assert.That(node.Status, Is.EqualTo(ResearchStatus.Locked));
            Assert.That(node.StatusText, Is.EqualTo("Requires: Sharpened Axes"));
            Assert.That(node.ShowsButton, Is.True);
            Assert.That(node.ButtonEnabled, Is.False);
            Assert.That(node.ButtonNote, Is.EqualTo("Requires: Sharpened Axes"));
        }

        /// <summary>Once the prerequisite is researched the node is available.</summary>
        [Test]
        public void Prerequisite_Done_UnlocksTheNode()
        {
            ResearchNodeModel node = Node("frame_saw", City(400, 0, 30, new[] { "sharp_axes" }));

            Assert.That(node.Status, Is.EqualTo(ResearchStatus.Available));
            Assert.That(node.ButtonEnabled, Is.True);
        }

        /// <summary>A short stock names every item the stock lacks, and only those.</summary>
        [Test]
        public void Available_WithShortStock_NamesWhatIsMissing()
        {
            ResearchNodeModel coinsOnly = Node("sharp_axes", City(249));
            ResearchNodeModel both = Node("frame_saw", City(399, 0, 29, new[] { "sharp_axes" }));
            ResearchNodeModel planksOnly = Node("frame_saw", City(400, 0, 29, new[] { "sharp_axes" }));

            Assert.That(coinsOnly.ButtonEnabled, Is.False);
            Assert.That(coinsOnly.ButtonNote, Is.EqualTo("Not enough Coins"));
            Assert.That(both.ButtonNote, Is.EqualTo("Not enough Coins, Planks"));
            Assert.That(planksOnly.ButtonNote, Is.EqualTo("Not enough Planks"));
        }

        /// <summary>While another research runs the button is off with the busy note, before the cost or a prerequisite is judged (the server's order).</summary>
        [Test]
        public void ResearchRunning_TurnsOtherButtonsOff_BeforeLockAndCost()
        {
            CityState city = City(0, 0, 0, null, HudTestData.Research("trade_charter", 500, 1100));

            Assert.That(Node("sharp_axes", city).ButtonNote, Is.EqualTo("Another research is running"));
            Assert.That(Node("frame_saw", city).ButtonNote, Is.EqualTo("Another research is running"));
            Assert.That(Node("sharp_axes", city).ButtonEnabled, Is.False);
        }

        /// <summary>The running node shows a ring with its progress and the time left, and has no button.</summary>
        [Test]
        public void InProgress_ShowsTheRingAndNoButton()
        {
            CityState city = HudTestData.City(1450, 0, 0, 0, new Building[0], null, HudTestData.Research("sharp_axes", 1000, 2800));

            ResearchNodeModel node = Node("sharp_axes", city);

            Assert.That(node.Status, Is.EqualTo(ResearchStatus.InProgress));
            Assert.That(node.ProgressPermille, Is.EqualTo(250));
            Assert.That(node.StatusText, Is.EqualTo("In progress · 22m 30s left"));
            Assert.That(node.ShowsButton, Is.False);
            Assert.That(node.ButtonEnabled, Is.False);
        }

        /// <summary>A researched node says so, shows no button and no ring.</summary>
        [Test]
        public void Done_HasNoButton()
        {
            ResearchNodeModel node = Node("sharp_axes", City(500, 0, 0, new[] { "sharp_axes" }));

            Assert.That(node.Status, Is.EqualTo(ResearchStatus.Done));
            Assert.That(node.StatusText, Is.EqualTo("Researched"));
            Assert.That(node.ShowsButton, Is.False);
            Assert.That(node.ProgressPermille, Is.EqualTo(0));
        }

        /// <summary>A start on its way switches this node's button off with its own note, and the others' with the busy note: a double tap cannot submit twice.</summary>
        [Test]
        public void PendingStart_TurnsButtonsOff()
        {
            CityState city = City(1000, 100);

            ResearchNodeModel sent = Node("sharp_axes", city, "sharp_axes");
            ResearchNodeModel other = Node("trade_charter", city, "sharp_axes");

            Assert.That(sent.ButtonEnabled, Is.False);
            Assert.That(sent.ButtonNote, Is.EqualTo("Starting research…"));
            Assert.That(other.ButtonEnabled, Is.False);
            Assert.That(other.ButtonNote, Is.EqualTo("Another research is running"));
        }

        /// <summary>The model carries the column it was given.</summary>
        [Test]
        public void Column_IsTheOneGiven()
        {
            Assert.That(Node("frame_saw", City(0), null, 1).Column, Is.EqualTo(1));
        }

        /// <summary>Two models made from the same city are equal; a changed stock changes the note and so the model.</summary>
        [Test]
        public void Equality_FollowsTheWords()
        {
            Assert.That(Node("sharp_axes", City(250)), Is.EqualTo(Node("sharp_axes", City(250))));
            Assert.That(Node("sharp_axes", City(250)), Is.Not.EqualTo(Node("sharp_axes", City(100))));
            Assert.That(Node("sharp_axes", City(250)).GetHashCode(), Is.EqualTo(Node("sharp_axes", City(250)).GetHashCode()));
        }
    }
}
