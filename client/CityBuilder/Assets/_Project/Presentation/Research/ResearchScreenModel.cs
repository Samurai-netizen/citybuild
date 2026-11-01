#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Research
{
    /// <summary>
    /// What the research screen (M0-52) says about the city's era: the header (<c>Early Middle Ages — 1 / 4 researched</c>), the rule that
    /// finishing every node advances the era, and one <see cref="ResearchNodeModel"/> per research node of the era, ordered by column (a node
    /// stands to the right of everything it requires) and, inside a column, in the content's order. It is made from the content, the displayed
    /// city and the id of a <c>StartResearch</c> still on its way, and keeps no state of its own. Immutable; two models that say the same compare
    /// equal. No Unity objects.
    /// </summary>
    public sealed class ResearchScreenModel : IEquatable<ResearchScreenModel>
    {
        /// <summary>The rule of the era, as the owner words it (<c>25</c> rule 4).</summary>
        public const string RuleText = "Complete all to advance";

        /// <summary>Creates a model from its parts; use <see cref="Create"/>.</summary>
        private ResearchScreenModel(string headerText, bool ruleIsActive, IReadOnlyList<ResearchNodeModel> nodes, int columnCount)
        {
            HeaderText = headerText;
            RuleIsActive = ruleIsActive;
            Nodes = nodes;
            ColumnCount = columnCount;
        }

        /// <summary>The era's name and how many of its nodes are researched, such as <c>Early Middle Ages — 1 / 4 researched</c>.</summary>
        public string HeaderText { get; }

        /// <summary>Whether finishing the era leads anywhere: false while the content has no later era (M0), and the screen then greys the rule out.</summary>
        public bool RuleIsActive { get; }

        /// <summary>The nodes of the city's era, by column and then in content order.</summary>
        public IReadOnlyList<ResearchNodeModel> Nodes { get; }

        /// <summary>The number of columns: one more than the largest <see cref="ResearchNodeModel.Column"/>; 0 when the era has no node.</summary>
        public int ColumnCount { get; }

        /// <summary>Makes the screen's words for <paramref name="city"/>, the city the client displays now.</summary>
        /// <param name="content">The game content.</param>
        /// <param name="city">The displayed city.</param>
        /// <param name="pendingResearchId">The id of a <c>StartResearch</c> on its way to the server, or null when none is.</param>
        /// <exception cref="ArgumentNullException">The content or the city is null.</exception>
        public static ResearchScreenModel Create(GameContent content, CityState city, string? pendingResearchId)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            var inEra = new List<ResearchDefinition>();
            for (int index = 0; index < content.Research.Count; index++)
            {
                if (content.Research[index].Era == city.Era)
                {
                    inEra.Add(content.Research[index]);
                }
            }

            var columns = new Dictionary<ResearchId, int>();
            int columnCount = 0;
            for (int index = 0; index < inEra.Count; index++)
            {
                columnCount = Math.Max(columnCount, ColumnOf(content, inEra[index], columns) + 1);
            }

            var nodes = new List<ResearchNodeModel>(inEra.Count);
            int researched = 0;
            for (int column = 0; column < columnCount; column++)
            {
                for (int index = 0; index < inEra.Count; index++)
                {
                    if (columns[inEra[index].Id] != column)
                    {
                        continue;
                    }

                    ResearchNodeModel node = ResearchNodeModel.Create(content, city, inEra[index], column, pendingResearchId);
                    researched += node.Status == ResearchStatus.Done ? 1 : 0;
                    nodes.Add(node);
                }
            }

            string eraName = content.TryGetEra(city.Era, out EraDefinition? era)
                ? era.Name
                : "Era " + city.Era.ToString(CultureInfo.InvariantCulture);
            string header = eraName + " — " + researched.ToString(CultureInfo.InvariantCulture) + " / "
                + nodes.Count.ToString(CultureInfo.InvariantCulture) + " researched";
            return new ResearchScreenModel(header, content.TryGetEra(city.Era + 1, out _), nodes.AsReadOnly(), columnCount);
        }

        /// <summary>True when the header, the rule and every node are the same: the view has nothing to redraw.</summary>
        public bool Equals(ResearchScreenModel? other)
        {
            if (other == null || HeaderText != other.HeaderText || RuleIsActive != other.RuleIsActive || Nodes.Count != other.Nodes.Count)
            {
                return false;
            }

            for (int index = 0; index < Nodes.Count; index++)
            {
                if (!Nodes[index].Equals(other.Nodes[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>See <see cref="Equals(ResearchScreenModel?)"/>.</summary>
        public override bool Equals(object? obj) => Equals(obj as ResearchScreenModel);

        /// <summary>A hash of the header and the node count, which go with <see cref="Equals(ResearchScreenModel?)"/>.</summary>
        public override int GetHashCode() => HeaderText.GetHashCode() ^ Nodes.Count;

        /// <summary>
        /// The column of a node: 0 when it requires nothing of its own era, else one more than the deepest prerequisite of its era. The content's
        /// rules guarantee the prerequisite graph has no cycle, so the recursion ends; earlier eras' nodes are not shown and take no column.
        /// </summary>
        private static int ColumnOf(GameContent content, ResearchDefinition node, Dictionary<ResearchId, int> columns)
        {
            if (columns.TryGetValue(node.Id, out int known))
            {
                return known;
            }

            int column = 0;
            for (int index = 0; index < node.Prerequisites.Count; index++)
            {
                if (content.TryGetResearch(node.Prerequisites[index], out ResearchDefinition? prerequisite) && prerequisite.Era == node.Era)
                {
                    column = Math.Max(column, ColumnOf(content, prerequisite, columns) + 1);
                }
            }

            columns[node.Id] = column;
            return column;
        }
    }
}
