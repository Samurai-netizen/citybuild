#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.Research;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.WelcomeBack
{
    /// <summary>
    /// What the welcome-back report says (M0-53): the title <c>While you were away (7 h 12 m)</c>, what was produced and used per item, which
    /// buildings were built or upgraded, and which research finished and what it does (the offline cap's note went with the cap, CR-0008-T1
    /// part H; the couriers' lines are CR-0008-T2's). It is made from the settlement block of one snapshot and
    /// from that snapshot's city, which names the buildings and their levels. <see cref="Create"/> gives nothing for a settlement shorter than
    /// <see cref="ThresholdSeconds"/> of real time, so a routine poll never shows a report, also on a Development server whose clock runs faster. Pure code; no Unity objects, so EditMode tests check every sentence.
    /// </summary>
    public sealed class WelcomeBackModel
    {
        /// <summary>The shortest absence that is reported, in real seconds: five minutes. A routine poll (every 120 s) settles far less.</summary>
        public const long ThresholdSeconds = 300;

        /// <summary>The line shown when nothing was produced, used or finished in the time away.</summary>
        public const string NothingHappenedText = "Nothing was produced or finished.";

        /// <summary>The heading of the items the city made.</summary>
        public const string ProducedHeading = "Produced";

        /// <summary>The heading of the items production took.</summary>
        public const string UsedHeading = "Used";

        /// <summary>The heading of the goods couriers and trip workers took out of stores.</summary>
        public const string MovedHeading = "Moved";

        /// <summary>The heading of the goods couriers and trip workers put into buildings and projects.</summary>
        public const string DeliveredHeading = "Delivered";

        /// <summary>The heading of the couriers' working time and tiredness.</summary>
        public const string CouriersHeading = "Couriers";

        /// <summary>The text of the report's main button when couriers spent stamina (the <c>reenergize-couriers</c> command).</summary>
        public const string ReenergizeText = "Re-energize couriers";

        /// <summary>The heading of the finished constructions and upgrades.</summary>
        public const string BuildingsHeading = "Buildings";

        /// <summary>The heading of the finished research.</summary>
        public const string ResearchHeading = "Research";

        /// <summary>Seconds in a minute and in an hour: units of time, not game numbers.</summary>
        private const long SecondsPerMinute = 60;

        /// <summary>Seconds in an hour.</summary>
        private const long SecondsPerHour = 3600;

        /// <summary>Creates the model; <see cref="Create"/> is the way in.</summary>
        private WelcomeBackModel(string title, IReadOnlyList<WelcomeBackSection> sections, bool canReenergize)
        {
            Title = title;
            Sections = sections;
            CanReenergize = canReenergize;
        }

        /// <summary>The title, such as <c>While you were away (7 h 12 m)</c>.</summary>
        public string Title { get; }

        /// <summary>The blocks that have something to say, in the order produced, used, buildings, research; empty when nothing happened.</summary>
        public IReadOnlyList<WelcomeBackSection> Sections { get; }

        /// <summary>Whether the report offers <see cref="ReenergizeText"/>: a courier spent stamina or got tired while the player was away.</summary>
        public bool CanReenergize { get; }

        /// <summary>Whether there is no section: <see cref="NothingHappenedText"/> is shown instead.</summary>
        public bool IsEmpty => Sections.Count == 0;

        /// <summary>
        /// Makes the report of <paramref name="snapshot"/>, or returns null when there is nothing to report: the snapshot has no settlement, or the
        /// settlement covers fewer than <see cref="ThresholdSeconds"/> real seconds: a settlement counts city seconds, and <paramref name="timeScale"/> of
        /// them pass per real second (M0-54).
        /// </summary>
        /// <param name="content">The game content the names come from.</param>
        /// <param name="snapshot">The snapshot the store took.</param>
        /// <param name="timeScale">The <c>timeScale</c> of <c>GET /content</c>, at least 1: 1 except on a Development server with a dev time scale.</param>
        /// <returns>The report, or null.</returns>
        /// <exception cref="ArgumentNullException">The content or the snapshot is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeScale"/> is below 1.</exception>
        public static WelcomeBackModel? Create(GameContent content, CitySnapshot snapshot, int timeScale)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (timeScale < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(timeScale), timeScale, "The time scale must be at least 1.");
            }

            SettlementSummary? settlement = snapshot.Settlement;
            if (settlement == null || settlement.ElapsedSeconds < ThresholdSeconds * timeScale)
            {
                return null;
            }

            var sections = new List<WelcomeBackSection>();
            AddSection(sections, ProducedHeading, Quantities(content, settlement.Produced));
            AddSection(sections, UsedHeading, Quantities(content, settlement.Consumed));
            AddSection(sections, MovedHeading, Quantities(content, settlement.Moved));
            AddSection(sections, DeliveredHeading, Quantities(content, settlement.Delivered));
            CityState city = snapshot.CopyCity();
            AddSection(sections, CouriersHeading, CourierLines(content, city, settlement, snapshot.Clock));
            AddSection(sections, BuildingsHeading, BuildingLines(content, city, settlement.CompletedBuildings));
            AddSection(sections, ResearchHeading, ResearchLines(content, settlement.CompletedResearch));
            bool canReenergize = settlement.CourierSeconds > 0 || settlement.TiredCouriers.Count > 0;
            return new WelcomeBackModel("While you were away (" + Duration(settlement.ElapsedSeconds) + ")", sections, canReenergize);
        }

        /// <summary>
        /// The couriers' lines: <c>Worked 6 h 40 m</c> for the longest working time, then <c>Courier 2 of Courier Station got tired after 3 h 12 m</c> for each
        /// courier whose stamina ran out, counted from the start of the time away (the city clock now minus the time the settlement covered).
        /// </summary>
        private static List<string> CourierLines(GameContent content, CityState city, SettlementSummary settlement, long clockNow)
        {
            var lines = new List<string>();
            if (settlement.CourierSeconds > 0)
            {
                lines.Add("Worked " + Duration(settlement.CourierSeconds));
            }

            long start = clockNow - settlement.ElapsedSeconds;
            foreach (TiredCourier tired in settlement.TiredCouriers)
            {
                string station = city.TryGetBuilding(tired.StationId, out Building? building) && content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition)
                    ? definition.Name
                    : "station " + tired.StationId.ToString(CultureInfo.InvariantCulture);
                lines.Add("Courier " + tired.Slot.ToString(CultureInfo.InvariantCulture) + " of " + station + " got tired after " + Duration(Math.Max(0, tired.At - start)));
            }

            return lines;
        }

        /// <summary>Adds the section unless it has no line.</summary>
        private static void AddSection(List<WelcomeBackSection> sections, string heading, List<string> lines)
        {
            if (lines.Count > 0)
            {
                sections.Add(new WelcomeBackSection(heading, lines));
            }
        }

        /// <summary><c>960 Logs</c> for each item, in the order the server sent them (sorted by item id).</summary>
        private static List<string> Quantities(GameContent content, IReadOnlyList<KeyValuePair<ItemId, long>> items)
        {
            var lines = new List<string>(items.Count);
            foreach (KeyValuePair<ItemId, long> item in items)
            {
                lines.Add(item.Value.ToString(CultureInfo.InvariantCulture) + " " + ItemText.ItemName(content, item.Key));
            }

            return lines;
        }

        /// <summary>
        /// <c>Sawmill built</c> or <c>Sawmill upgraded to level 2</c> for each completed building, by what the city shows: a construction completes
        /// at level 1 and an upgrade at level 2 or more. The same sentence for several buildings is one line with a count (<c>Cottage built ×2</c>), in the
        /// order its first building was listed. A building the city does not hold (it never happens in a real answer) is left out.
        /// </summary>
        private static List<string> BuildingLines(GameContent content, CityState city, IReadOnlyList<int> completed)
        {
            var sentences = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (int id in completed)
            {
                if (!city.TryGetBuilding(id, out Building? building))
                {
                    continue;
                }

                string name = content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition) ? definition.Name : building.TypeId.Value;
                string sentence = building.Level <= 1
                    ? name + " built"
                    : name + " upgraded to level " + building.Level.ToString(CultureInfo.InvariantCulture);
                if (counts.TryGetValue(sentence, out int count))
                {
                    counts[sentence] = count + 1;
                }
                else
                {
                    counts[sentence] = 1;
                    sentences.Add(sentence);
                }
            }

            var lines = new List<string>(sentences.Count);
            foreach (string sentence in sentences)
            {
                int count = counts[sentence];
                lines.Add(count == 1 ? sentence : sentence + " ×" + count.ToString(CultureInfo.InvariantCulture));
            }

            return lines;
        }

        /// <summary><c>Sharpened Axes — Woodcutter's Hut: −20% labor</c> for each finished research; just the name when it has no effect with words.</summary>
        private static List<string> ResearchLines(GameContent content, IReadOnlyList<ResearchId> completed)
        {
            var lines = new List<string>(completed.Count);
            foreach (ResearchId id in completed)
            {
                if (!content.TryGetResearch(id, out ResearchDefinition? definition))
                {
                    lines.Add(id.Value);
                    continue;
                }

                string effect = ResearchEffectText.Of(content, definition);
                lines.Add(effect.Length == 0 ? definition.Name : definition.Name + " — " + effect);
            }

            return lines;
        }

        /// <summary>
        /// A span as hours and minutes with a space before the unit: <c>7 h 12 m</c>, <c>8 h</c>, <c>45 m</c>; the seconds are dropped from a span of a
        /// minute or more, and a span under a minute is <c>30 s</c>. The largest unit is the hour, as in <c>DurationText</c>.
        /// </summary>
        private static string Duration(long seconds)
        {
            if (seconds < SecondsPerMinute)
            {
                return Math.Max(0, seconds).ToString(CultureInfo.InvariantCulture) + " s";
            }

            long hours = seconds / SecondsPerHour;
            long minutes = seconds % SecondsPerHour / SecondsPerMinute;
            string hourText = hours > 0 ? hours.ToString(CultureInfo.InvariantCulture) + " h" : string.Empty;
            string minuteText = minutes > 0 ? minutes.ToString(CultureInfo.InvariantCulture) + " m" : string.Empty;
            return hourText.Length > 0 && minuteText.Length > 0 ? hourText + " " + minuteText : hourText + minuteText;
        }
    }
}
