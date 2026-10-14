#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's checks of single numbers: amounts in recipes, bonuses and caps, labor, durations, the grid and slots are greater than
    /// zero; amounts in costs and the starting inventory are at least zero; permille stays within 1000; M0 has exactly one research slot.
    /// </summary>
    internal static class ValueRules
    {
        // The M0 engine holds one active research (core-instructions/04-simulation-model.md, section 5); more slots are a later minor mechanic.
        private const int _supportedResearchSlots = 1;

        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            CheckRules(draft.Rules, errors);
            AtLeastZero(draft.StartingCity.Inventory, "startingCity.inventory", errors);
            foreach (BuildingDefinition building in draft.Buildings)
            {
                CheckBuilding(building, errors);
            }

            foreach (ResearchDefinition node in draft.Research)
            {
                CheckResearch(node, errors);
            }
        }

        private static void CheckRules(GameRules rules, ContentErrorList errors)
        {
            Positive(rules.GridWidth, "rules.gridWidth", errors);
            Positive(rules.GridHeight, "rules.gridHeight", errors);
            Positive(rules.BuilderSlots, "rules.builderSlots", errors);
            Positive(rules.ResearchSlots, "rules.researchSlots", errors);
            if (rules.ResearchSlots > _supportedResearchSlots)
            {
                errors.Add(
                    ContentErrorCode.ValueTooLarge,
                    "rules.researchSlots",
                    FormattableString.Invariant($"The engine holds one active research in M0, so researchSlots must be {_supportedResearchSlots}, but is {rules.ResearchSlots}."));
            }

            Positive(rules.MaxOfflineProductionSeconds, "rules.maxOfflineProductionSeconds", errors);
            Positive(rules.MinCycleSeconds, "rules.minCycleSeconds", errors);
            AtLeastZero(rules.MaxLaborReductionPermille, "rules.maxLaborReductionPermille", errors);
            AtMostOneThousand(rules.MaxLaborReductionPermille, "rules.maxLaborReductionPermille", errors);
            Positive(rules.BaseStorageCaps, "rules.baseStorageCaps", errors);
        }

        private static void CheckBuilding(BuildingDefinition building, ContentErrorList errors)
        {
            string path = ContentPaths.Building(building);
            AtLeastZero(building.Cost, path + ".cost", errors);
            Positive(building.BuildSeconds, path + ".buildSeconds", errors);
            foreach (BuildingLevelDefinition level in building.Levels)
            {
                string levelPath = ContentPaths.Level(building, level);
                if (level.Level > 1)
                {
                    AtLeastZero(level.UpgradeCost, levelPath + ".upgradeCost", errors);
                    Positive(level.UpgradeSeconds, levelPath + ".upgradeSeconds", errors);
                }

                if (level.Recipe is not null)
                {
                    Positive(level.Recipe.Inputs, levelPath + ".recipe.inputs", errors);
                    Positive(level.Recipe.Outputs, levelPath + ".recipe.outputs", errors);
                    Positive(level.Recipe.Labor, levelPath + ".recipe.labor", errors);
                }
            }
        }

        private static void CheckResearch(ResearchDefinition node, ContentErrorList errors)
        {
            string path = ContentPaths.Research(node);
            AtLeastZero(node.Cost, path + ".cost", errors);
            Positive(node.DurationSeconds, path + ".durationSeconds", errors);
            for (int index = 0; index < node.Effects.Count; index++)
            {
                string effectPath = ContentPaths.Effect(node, index);
                switch (node.Effects[index])
                {
                    case LaborReductionEffect reduction:
                        Positive(reduction.Permille, effectPath + ".permille", errors);
                        AtMostOneThousand(reduction.Permille, effectPath + ".permille", errors);
                        break;
                    case StorageCapBonusEffect bonus:
                        Positive(bonus.Amount, effectPath + ".amount", errors);
                        break;
                }
            }
        }

        private static void Positive(long value, string path, ContentErrorList errors)
        {
            if (value <= 0)
            {
                errors.Add(ContentErrorCode.NotPositive, path, FormattableString.Invariant($"Must be greater than 0, but is {value}."));
            }
        }

        private static void Positive(IReadOnlyList<ItemQuantity> amounts, string path, ContentErrorList errors)
        {
            foreach (ItemQuantity amount in amounts)
            {
                Positive(amount.Quantity, path + "." + amount.Item.Value, errors);
            }
        }

        private static void AtLeastZero(long value, string path, ContentErrorList errors)
        {
            if (value < 0)
            {
                errors.Add(ContentErrorCode.Negative, path, FormattableString.Invariant($"Must be 0 or more, but is {value}."));
            }
        }

        private static void AtLeastZero(IReadOnlyList<ItemQuantity> amounts, string path, ContentErrorList errors)
        {
            foreach (ItemQuantity amount in amounts)
            {
                AtLeastZero(amount.Quantity, path + "." + amount.Item.Value, errors);
            }
        }

        private static void AtMostOneThousand(int permille, string path, ContentErrorList errors)
        {
            if (permille > IntegerMath.PermilleScale)
            {
                errors.Add(
                    ContentErrorCode.ValueTooLarge,
                    path,
                    FormattableString.Invariant($"A reduction is at most {IntegerMath.PermilleScale} permille (all of the labor), but is {permille}."));
            }
        }
    }
}
