#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.WelcomeBack;
using CityBuilder.Client.Tests.EditMode.Core;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.WelcomeBack
{
    /// <summary>
    /// The welcome-back report (M0-53): when it appears (never for a settlement under five minutes), every sentence of the title, the produced and used
    /// lines, the buildings, the research with its effect and the production-cap note, and the sections it leaves out.
    /// </summary>
    public sealed class WelcomeBackModelTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>A settlement with the given times and lists, which the tests fill in as they need.</summary>
        private static SettlementSummary Settlement(
            long elapsed,
            long? production = null,
            (string Item, long Quantity)[]? produced = null,
            (string Item, long Quantity)[]? consumed = null,
            int[]? buildings = null,
            string[]? research = null)
        {
            return new SettlementSummary(
                elapsed,
                production ?? elapsed,
                Quantities(produced),
                Quantities(consumed),
                buildings ?? new int[0],
                (research ?? new string[0]).Select(id => new ResearchId(id)).ToList(),
                false);
        }

        /// <summary>The engine's pairs of the (item, quantity) tuples.</summary>
        private static List<KeyValuePair<ItemId, long>> Quantities((string Item, long Quantity)[]? lines)
        {
            return (lines ?? new (string, long)[0]).Select(line => new KeyValuePair<ItemId, long>(new ItemId(line.Item), line.Quantity)).ToList();
        }

        /// <summary>A snapshot of a city with the given buildings, carrying <paramref name="settlement"/>.</summary>
        private static CitySnapshot Snapshot(SettlementSummary? settlement, params Building[] buildings)
        {
            return new CitySnapshot(5, "mvp-1", HudTestData.City(100_000, 10, 0, 0, buildings), settlement);
        }

        /// <summary>A building of <paramref name="typeId"/> that is finished at <paramref name="level"/>.</summary>
        private static Building Finished(int id, string typeId, int level)
        {
            return new Building(id, new BuildingTypeId(typeId), id, 0, level, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        }

        /// <summary>The report of <paramref name="settlement"/> over the given buildings; it must exist.</summary>
        private WelcomeBackModel Report(SettlementSummary settlement, params Building[] buildings)
        {
            WelcomeBackModel? model = WelcomeBackModel.Create(_content, Snapshot(settlement, buildings));
            Assert.That(model, Is.Not.Null);
            return model!;
        }

        /// <summary>The lines of the section with the given heading.</summary>
        private static string[] Lines(WelcomeBackModel model, string heading)
        {
            return model.Sections.Where(section => section.Heading == heading).SelectMany(section => section.Lines).ToArray();
        }

        /// <summary>A snapshot with no settlement block has nothing to report.</summary>
        [Test]
        public void Create_NoSettlement_IsNull()
        {
            Assert.That(WelcomeBackModel.Create(_content, Snapshot(null)), Is.Null);
        }

        /// <summary>The edge: 299 s is a routine settlement, 300 s is a report.</summary>
        [Test]
        public void Create_FiveMinutesIsTheEdge()
        {
            Assert.That(WelcomeBackModel.Create(_content, Snapshot(Settlement(299))), Is.Null);
            Assert.That(WelcomeBackModel.Create(_content, Snapshot(Settlement(300))), Is.Not.Null);
            Assert.That(WelcomeBackModel.ThresholdSeconds, Is.EqualTo(300L));
        }

        /// <summary>A routine poll's settlement (about two minutes) with plenty produced is still no report.</summary>
        [Test]
        public void Create_ARoutinePoll_IsNullEvenWhenSomethingHappened()
        {
            SettlementSummary routine = Settlement(121, produced: new[] { ("logs", 30L) }, buildings: new[] { 1 }, research: new[] { "frame_saw" });

            Assert.That(WelcomeBackModel.Create(_content, Snapshot(routine, Finished(1, "house", 1))), Is.Null);
        }

        /// <summary>The title says the time in hours and minutes, the seconds dropped.</summary>
        [TestCase(300L, "While you were away (5 m)")]
        [TestCase(359L, "While you were away (5 m)")]
        [TestCase(3600L, "While you were away (1 h)")]
        [TestCase(3660L, "While you were away (1 h 1 m)")]
        [TestCase(25_920L, "While you were away (7 h 12 m)")]
        [TestCase(28_800L, "While you were away (8 h)")]
        [TestCase(180_000L, "While you were away (50 h)")]
        public void Title_SaysTheTimeAway(long elapsed, string expected)
        {
            Assert.That(Report(Settlement(elapsed)).Title, Is.EqualTo(expected));
        }

        /// <summary>What was produced and what was used are two sections, one line per item with the content's name, in the order sent.</summary>
        [Test]
        public void Items_ArePerItemWithContentNames()
        {
            WelcomeBackModel model = Report(Settlement(
                25_920,
                produced: new[] { ("logs", 960L), ("planks", 120L) },
                consumed: new[] { ("logs", 940L) }));

            Assert.That(Lines(model, WelcomeBackModel.ProducedHeading), Is.EqualTo(new[] { "960 Logs", "120 Planks" }));
            Assert.That(Lines(model, WelcomeBackModel.UsedHeading), Is.EqualTo(new[] { "940 Logs" }));
        }

        /// <summary>The sections come in the order produced, used, buildings, research; one with nothing to say is left out.</summary>
        [Test]
        public void Sections_AreInOrderAndSkipTheEmptyOnes()
        {
            WelcomeBackModel all = Report(
                Settlement(600, produced: new[] { ("logs", 1L) }, consumed: new[] { ("logs", 1L) }, buildings: new[] { 1 }, research: new[] { "frame_saw" }),
                Finished(1, "sawmill", 1));
            WelcomeBackModel onlyUsed = Report(Settlement(600, consumed: new[] { ("logs", 5L) }));

            Assert.That(
                all.Sections.Select(section => section.Heading),
                Is.EqualTo(new[] { "Produced", "Used", "Buildings", "Research" }));
            Assert.That(onlyUsed.Sections.Select(section => section.Heading), Is.EqualTo(new[] { "Used" }));
            Assert.That(onlyUsed.IsEmpty, Is.False);
        }

        /// <summary>A construction that completed (level 1) reads "built"; an upgrade (level 2 or more) says its level.</summary>
        [Test]
        public void Buildings_ConstructionsAndUpgrades()
        {
            WelcomeBackModel model = Report(
                Settlement(600, buildings: new[] { 1, 2, 3 }),
                Finished(1, "sawmill", 1),
                Finished(2, "woodcutter", 2),
                Finished(3, "house", 3));

            Assert.That(
                Lines(model, WelcomeBackModel.BuildingsHeading),
                Is.EqualTo(new[] { "Sawmill built", "Woodcutter's Hut upgraded to level 2", "Cottage upgraded to level 3" }));
        }

        /// <summary>The same sentence for several buildings is one line with a count, in the order its first building was listed.</summary>
        [Test]
        public void Buildings_TheSameSentenceIsCounted()
        {
            WelcomeBackModel model = Report(
                Settlement(600, buildings: new[] { 1, 2, 3, 4 }),
                Finished(1, "house", 1),
                Finished(2, "sawmill", 1),
                Finished(3, "house", 1),
                Finished(4, "house", 1));

            Assert.That(Lines(model, WelcomeBackModel.BuildingsHeading), Is.EqualTo(new[] { "Cottage built ×3", "Sawmill built" }));
        }

        /// <summary>A building that started its next upgrade after the settlement still reads as the level it reached.</summary>
        [Test]
        public void Buildings_UpgradingAgainStillNamesTheLevelReached()
        {
            var upgrading = new Building(2, new BuildingTypeId("sawmill"), 2, 0, 2, 0, BuildingPhase.Upgrading, 100, 200, 3, 0, 0, 0);

            WelcomeBackModel model = Report(Settlement(600, buildings: new[] { 2 }), upgrading);

            Assert.That(Lines(model, WelcomeBackModel.BuildingsHeading), Is.EqualTo(new[] { "Sawmill upgraded to level 2" }));
        }

        /// <summary>A completed building the city does not hold is left out, and the section with it.</summary>
        [Test]
        public void Buildings_UnknownIdIsSkipped()
        {
            WelcomeBackModel model = Report(Settlement(600, buildings: new[] { 99 }), Finished(1, "house", 1));

            Assert.That(model.Sections, Is.Empty);
        }

        /// <summary>A research line gives the name and what it does.</summary>
        [Test]
        public void Research_NamesTheEffect()
        {
            WelcomeBackModel model = Report(Settlement(600, research: new[] { "sharp_axes", "trade_charter", "storehouses" }));

            Assert.That(
                Lines(model, WelcomeBackModel.ResearchHeading),
                Is.EqualTo(new[]
                {
                    "Sharpened Axes — Woodcutter's Hut: −20% labor",
                    "Trade Charter — Unlocks Trading Post",
                    "Storehouses — +250 Logs storage, +250 Planks storage",
                }));
        }

        /// <summary>A research the content does not know is listed by its id rather than dropped.</summary>
        [Test]
        public void Research_UnknownIdIsListedById()
        {
            WelcomeBackModel model = Report(Settlement(600, research: new[] { "lost_art" }));

            Assert.That(Lines(model, WelcomeBackModel.ResearchHeading), Is.EqualTo(new[] { "lost_art" }));
        }

        /// <summary>When production counted fewer seconds than passed, the note names the hours of production and what to do.</summary>
        [Test]
        public void CapNote_WhenProductionWasCut()
        {
            WelcomeBackModel model = Report(Settlement(36_000, production: 28_800));

            Assert.That(model.HasCapNote, Is.True);
            Assert.That(model.CapNote, Is.EqualTo("Production paused after 8 h. Check in more often or research storage."));
            Assert.That(model.Title, Is.EqualTo("While you were away (10 h)"));
        }

        /// <summary>The note names the minutes of a cap that is not a whole number of hours, and the seconds of a very short one.</summary>
        [Test]
        public void CapNote_NamesMinutesAndSeconds()
        {
            Assert.That(Report(Settlement(40_000, production: 30_600)).CapNote, Does.StartWith("Production paused after 8 h 30 m. "));
            Assert.That(Report(Settlement(400, production: 30)).CapNote, Does.StartWith("Production paused after 30 s. "));
        }

        /// <summary>No note when production ran for the whole time, also exactly at the cap.</summary>
        [Test]
        public void CapNote_NotWhenProductionRanTheWholeTime()
        {
            WelcomeBackModel model = Report(Settlement(28_800, production: 28_800));

            Assert.That(model.HasCapNote, Is.False);
            Assert.That(model.CapNote, Is.Empty);
        }

        /// <summary>A long absence in which nothing happened still gets a report: the title, no section, and the empty flag.</summary>
        [Test]
        public void NothingHappened_IsEmptyButStillReported()
        {
            WelcomeBackModel model = Report(Settlement(3600));

            Assert.That(model.IsEmpty, Is.True);
            Assert.That(model.Sections, Is.Empty);
            Assert.That(model.Title, Is.EqualTo("While you were away (1 h)"));
            Assert.That(WelcomeBackModel.NothingHappenedText, Is.Not.Empty);
        }

        /// <summary>The report of a real answer (429 s, a research completed, items made and used) reads as the server's numbers.</summary>
        [Test]
        public void RealAnswer_ReadsAsTheServerSaid()
        {
            CitySnapshot snapshot = CityTestData.Snapshot("snapshot-city-research-completed.json");

            WelcomeBackModel model = WelcomeBackModel.Create(_content, snapshot)!;

            Assert.That(model.Title, Is.EqualTo("While you were away (7 m)"));
            Assert.That(Lines(model, "Produced"), Is.EqualTo(new[] { "14 Logs", "7 Planks" }));
            Assert.That(Lines(model, "Used"), Is.EqualTo(new[] { "14 Logs" }));
            Assert.That(Lines(model, "Research"), Is.EqualTo(new[] { "Trade Charter — Unlocks Trading Post" }));
            Assert.That(model.HasCapNote, Is.False);
        }

        /// <summary>A real answer of 42 s that completed two cottages is a routine settlement: no report.</summary>
        [Test]
        public void RealAnswer_ShortSettlement_IsNull()
        {
            Assert.That(WelcomeBackModel.Create(_content, CityTestData.Snapshot("snapshot-city-settled-buildings.json")), Is.Null);
        }

        /// <summary>Missing arguments are coding errors.</summary>
        [Test]
        public void Create_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => WelcomeBackModel.Create(null!, Snapshot(null)));
            Assert.Throws<ArgumentNullException>(() => WelcomeBackModel.Create(_content, null!));
        }

        /// <summary>A section copies its lines, refuses none, and needs a heading.</summary>
        [Test]
        public void Section_CopiesItsLinesAndRefusesNone()
        {
            var lines = new List<string> { "a" };
            var section = new WelcomeBackSection("Produced", lines);
            lines.Add("b");

            Assert.That(section.Lines, Is.EqualTo(new[] { "a" }));
            Assert.Throws<ArgumentException>(() => new WelcomeBackSection("Produced", new string[0]));
            Assert.Throws<ArgumentNullException>(() => new WelcomeBackSection(null!, new[] { "a" }));
            Assert.Throws<ArgumentNullException>(() => new WelcomeBackSection("Produced", null!));
        }
    }
}
