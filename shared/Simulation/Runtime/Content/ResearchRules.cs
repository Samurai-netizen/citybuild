#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's checks of the research graph: no cycle of prerequisites (a node on one could never start), and no prerequisite from a
    /// later era than the node (a city can only start the research of its era, so the node would be locked for good). Unknown prerequisites and
    /// duplicate nodes are reported by the reference rules and left out here.
    /// </summary>
    internal static class ResearchRules
    {
        private enum VisitState
        {
            InProgress,
            Done,
        }

        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            CheckCycles(draft, errors);
            CheckPrerequisiteEras(draft, errors);
        }

        private static void CheckCycles(ContentDraft draft, ContentErrorList errors)
        {
            // Depth-first search in ordinal id order (prerequisites are sorted too), so the same content reports the same cycles.
            var visits = new Dictionary<ResearchId, VisitState>();
            var stack = new List<ResearchId>();
            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst).OrderBy(node => node.Id))
            {
                Visit(draft, node, visits, stack, errors);
            }
        }

        private static void Visit(ContentDraft draft, ResearchDefinition node, Dictionary<ResearchId, VisitState> visits, List<ResearchId> stack, ContentErrorList errors)
        {
            if (visits.ContainsKey(node.Id))
            {
                return;
            }

            visits.Add(node.Id, VisitState.InProgress);
            stack.Add(node.Id);
            foreach (ResearchId prerequisite in node.Prerequisites.Distinct())
            {
                if (!draft.ResearchById.TryGetValue(prerequisite, out ResearchDefinition? required))
                {
                    continue;
                }

                if (visits.TryGetValue(prerequisite, out VisitState state) && state == VisitState.InProgress)
                {
                    ReportCycle(node, prerequisite, stack, errors);
                }
                else
                {
                    Visit(draft, required, visits, stack, errors);
                }
            }

            stack.RemoveAt(stack.Count - 1);
            visits[node.Id] = VisitState.Done;
        }

        private static void ReportCycle(ResearchDefinition node, ResearchId backTo, List<ResearchId> stack, ContentErrorList errors)
        {
            // The stack runs from the search's start to this node; the cycle is its part from backTo onwards, closed by backTo again.
            IEnumerable<ResearchId> cycle = stack.Skip(stack.IndexOf(backTo)).Append(backTo);
            errors.Add(
                ContentErrorCode.ResearchCycle,
                ContentPaths.Prerequisite(node, backTo),
                "The prerequisites form a cycle (each requires the next): " + string.Join(" → ", cycle.Select(id => id.Value))
                + "; none of these nodes could ever start.");
        }

        private static void CheckPrerequisiteEras(ContentDraft draft, ContentErrorList errors)
        {
            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                foreach (ResearchId prerequisite in node.Prerequisites.Distinct())
                {
                    // Unknown nodes and unknown eras are reported by the reference rules; their order means nothing.
                    if (draft.ResearchById.TryGetValue(prerequisite, out ResearchDefinition? required)
                        && draft.ErasByNumber.ContainsKey(node.Era)
                        && draft.ErasByNumber.ContainsKey(required.Era)
                        && required.Era > node.Era)
                    {
                        errors.Add(
                            ContentErrorCode.PrerequisiteInLaterEra,
                            ContentPaths.Prerequisite(node, prerequisite),
                            FormattableString.Invariant(
                                $"{node.Id.Value} (era {node.Era}) requires {prerequisite.Value} of the later era {required.Era}; a city leaves era {node.Era} only after all of its research, so {node.Id.Value} could never start."));
                    }
                }
            }
        }
    }
}
