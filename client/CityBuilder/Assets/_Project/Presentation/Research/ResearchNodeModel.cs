#nullable enable

using System;
using System.Text;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Research
{
    /// <summary>
    /// What the research screen (M0-52) says about one node of the era's tree, as plain text and flags: name, effect, cost, duration, the status
    /// derived from the displayed city (locked with the missing prerequisites, available, in progress with its ring, done) and the Finance
    /// button with the reason it is off. Everything comes from the content, the city and the id of a <c>StartResearch</c> still on its way to the
    /// server. The button's reasons follow the server's order (<c>CityRules.StartResearch</c>: a research is running, a prerequisite is
    /// missing, the stock is short) and are hints only: the server checks again. Immutable; two models that say the same compare equal, so the
    /// screen redraws only what differs. No Unity objects: EditMode tests check every sentence.
    /// </summary>
    public sealed class ResearchNodeModel : IEquatable<ResearchNodeModel>
    {
        /// <summary>The Finance button's note while this node's start is on its way to the server.</summary>
        public const string StartingNote = "Starting research…";

        /// <summary>The Finance button's note while another research runs or is about to start.</summary>
        public const string BusyNote = "Another research is running";

        /// <summary>Creates an empty model; <see cref="Create"/> fills it.</summary>
        private ResearchNodeModel()
        {
        }

        /// <summary>The node's id: what the Finance button's command names.</summary>
        public string Id { get; private set; } = string.Empty;

        /// <summary>The node's name from the content, such as <c>Sharpened Axes</c>.</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>What completing the node does, such as <c>Unlocks Trading Post</c> or <c>Woodcutter: −20% labor</c>; the effects are joined by a comma.</summary>
        public string EffectText { get; private set; } = string.Empty;

        /// <summary>The price, such as <c>Cost: 250 Coins</c>, or <c>Free</c> for a node with no cost.</summary>
        public string CostText { get; private set; } = string.Empty;

        /// <summary>The duration, such as <c>Takes 30m</c>.</summary>
        public string TimeText { get; private set; } = string.Empty;

        /// <summary>Where the node stands.</summary>
        public ResearchStatus Status { get; private set; }

        /// <summary>The status in words: <c>Requires: Sharpened Axes</c>, <c>Ready to research</c>, <c>In progress · 12m 30s left</c> or <c>Researched</c>.</summary>
        public string StatusText { get; private set; } = string.Empty;

        /// <summary>The column of the layout: 0 for a node with no prerequisite in the era, else one more than the deepest prerequisite's.</summary>
        public int Column { get; private set; }

        /// <summary>How far the running research has come, 0 to 1000; 0 for a node that is not running.</summary>
        public int ProgressPermille { get; private set; }

        /// <summary>Whether the Finance button is shown: a node that is locked or available (a running or researched node has nothing to finance).</summary>
        public bool ShowsButton { get; private set; }

        /// <summary>Whether the Finance button works.</summary>
        public bool ButtonEnabled { get; private set; }

        /// <summary>Why the button is off, such as <c>Not enough Coins</c>; empty while it works or is not shown.</summary>
        public string ButtonNote { get; private set; } = string.Empty;

        /// <summary>
        /// Makes the words of <paramref name="node"/> for <paramref name="city"/>, the city the client displays now (its clock is the clock shown).
        /// </summary>
        /// <param name="content">The game content the names come from.</param>
        /// <param name="city">The displayed city.</param>
        /// <param name="node">A research node of the content.</param>
        /// <param name="column">The node's column in the layout (<see cref="ResearchScreenModel"/> works it out for the whole tree).</param>
        /// <param name="pendingResearchId">The id of a <c>StartResearch</c> on its way to the server, or null when none is.</param>
        /// <param name="availability">Whether a command may be sent now (<see cref="CommandAvailability.For"/>): when it may not, the button is off and its note is the reason (M0-55).</param>
        /// <exception cref="ArgumentNullException">The content, the city or the node is null.</exception>
        public static ResearchNodeModel Create(
            GameContent content,
            CityState city,
            ResearchDefinition node,
            int column,
            string? pendingResearchId,
            CommandAvailability availability)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            string missing = MissingPrerequisites(content, city, node);
            ResearchStatus status = StatusOf(city, node, missing);
            var model = new ResearchNodeModel
            {
                Id = node.Id.Value,
                Name = node.Name,
                EffectText = ResearchEffectText.Of(content, node),
                CostText = node.Cost.Count == 0 ? "Free" : "Cost: " + ItemText.Quantities(content, node.Cost, ", "),
                TimeText = "Takes " + DurationText.Format(node.DurationSeconds),
                Status = status,
                Column = column,
            };

            switch (status)
            {
                case ResearchStatus.Done:
                    model.StatusText = "Researched";
                    break;
                case ResearchStatus.InProgress:
                    ActiveResearch running = city.ActiveResearch!;
                    model.ProgressPermille = ProgressDisplay.ResearchProgressPermille(city, city.Clock);
                    model.StatusText = "In progress · " + DurationText.Format(Math.Max(0, running.EndsAt - city.Clock)) + " left";
                    break;
                case ResearchStatus.Locked:
                    model.StatusText = "Requires: " + missing;
                    break;
                default:
                    model.StatusText = "Ready to research";
                    break;
            }

            model.ShowsButton = status == ResearchStatus.Locked || status == ResearchStatus.Available;
            if (model.ShowsButton)
            {
                model.ButtonNote = availability.IsAllowed ? ButtonNoteOf(content, city, node, pendingResearchId, missing) : availability.Reason;
                model.ButtonEnabled = model.ButtonNote.Length == 0;
            }

            return model;
        }

        /// <summary>True when every text and flag is the same: the view has nothing to redraw.</summary>
        public bool Equals(ResearchNodeModel? other)
        {
            return other != null
                && Id == other.Id
                && Name == other.Name
                && EffectText == other.EffectText
                && CostText == other.CostText
                && TimeText == other.TimeText
                && Status == other.Status
                && StatusText == other.StatusText
                && Column == other.Column
                && ProgressPermille == other.ProgressPermille
                && ShowsButton == other.ShowsButton
                && ButtonEnabled == other.ButtonEnabled
                && ButtonNote == other.ButtonNote;
        }

        /// <summary>See <see cref="Equals(ResearchNodeModel?)"/>.</summary>
        public override bool Equals(object? obj) => Equals(obj as ResearchNodeModel);

        /// <summary>A hash of the id and the texts that go with <see cref="Equals(ResearchNodeModel?)"/>.</summary>
        public override int GetHashCode() => Id.GetHashCode() ^ StatusText.GetHashCode() ^ ButtonNote.GetHashCode();

        /// <summary>The names of the prerequisites the city has not researched, joined by a comma; empty when none is missing.</summary>
        private static string MissingPrerequisites(GameContent content, CityState city, ResearchDefinition node)
        {
            var missing = new StringBuilder();
            for (int index = 0; index < node.Prerequisites.Count; index++)
            {
                ResearchId prerequisite = node.Prerequisites[index];
                if (city.HasCompletedResearch(prerequisite))
                {
                    continue;
                }

                string name = content.TryGetResearch(prerequisite, out ResearchDefinition? definition) ? definition.Name : prerequisite.Value;
                missing.Append(missing.Length == 0 ? string.Empty : ", ").Append(name);
            }

            return missing.ToString();
        }

        /// <summary>Done, in progress, locked or available, in that order of precedence.</summary>
        private static ResearchStatus StatusOf(CityState city, ResearchDefinition node, string missing)
        {
            if (city.HasCompletedResearch(node.Id))
            {
                return ResearchStatus.Done;
            }

            if (city.ActiveResearch != null && city.ActiveResearch.Id == node.Id)
            {
                return ResearchStatus.InProgress;
            }

            return missing.Length > 0 ? ResearchStatus.Locked : ResearchStatus.Available;
        }

        /// <summary>The first reason the button is off, in the server's order, or empty when it works.</summary>
        private static string ButtonNoteOf(
            GameContent content, CityState city, ResearchDefinition node, string? pendingResearchId, string missing)
        {
            if (pendingResearchId == node.Id.Value)
            {
                return StartingNote;
            }

            if (city.ActiveResearch != null || pendingResearchId != null)
            {
                return BusyNote;
            }

            if (missing.Length > 0)
            {
                return "Requires: " + missing;
            }

            var lacking = new StringBuilder();
            for (int index = 0; index < node.Cost.Count; index++)
            {
                if (city.GetQuantity(node.Cost[index].Item) < node.Cost[index].Quantity)
                {
                    lacking.Append(lacking.Length == 0 ? "Not enough " : ", ").Append(ItemText.ItemName(content, node.Cost[index].Item));
                }
            }

            return lacking.ToString();
        }
    }
}
