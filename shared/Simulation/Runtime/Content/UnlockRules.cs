#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's unlock check: a building type that is not available at the start is unlocked by exactly one research node, and one
    /// that is available is unlocked by none. The unlock is stated only in the research effect, so this is what keeps it unambiguous.
    /// </summary>
    internal static class UnlockRules
    {
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            var unlockedBy = new Dictionary<BuildingTypeId, ResearchId>();
            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                for (int index = 0; index < node.Effects.Count; index++)
                {
                    // An unknown building type is reported by the reference rules.
                    if (node.Effects[index] is UnlockBuildingEffect unlock && draft.BuildingsById.TryGetValue(unlock.Building, out BuildingDefinition? building))
                    {
                        CheckUnlock(node, index, building, unlockedBy, errors);
                    }
                }
            }

            foreach (BuildingDefinition building in draft.Buildings.Where(draft.IsFirst))
            {
                if (!building.IsAvailableAtStart && !unlockedBy.ContainsKey(building.Id))
                {
                    errors.Add(
                        ContentErrorCode.UnlockMismatch,
                        ContentPaths.Building(building),
                        building.Id.Value + " is not available at the start, but no research node unlocks it (an unlockBuilding effect).");
                }
            }
        }

        private static void CheckUnlock(
            ResearchDefinition node, int index, BuildingDefinition building, Dictionary<BuildingTypeId, ResearchId> unlockedBy, ContentErrorList errors)
        {
            string path = ContentPaths.Effect(node, index);
            if (building.IsAvailableAtStart)
            {
                errors.Add(
                    ContentErrorCode.UnlockMismatch,
                    path,
                    building.Id.Value + " is available at the start, so no research may unlock it; set availableAtStart to false or remove the effect.");
            }
            else if (unlockedBy.TryGetValue(building.Id, out ResearchId first))
            {
                errors.Add(
                    ContentErrorCode.UnlockMismatch,
                    path,
                    building.Id.Value + " is already unlocked by " + first.Value + "; exactly one research node unlocks a building type.");
            }
            else
            {
                unlockedBy.Add(building.Id, node.Id);
            }
        }
    }
}
