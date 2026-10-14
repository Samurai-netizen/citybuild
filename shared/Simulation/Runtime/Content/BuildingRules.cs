#nullable enable
using System;
using System.Globalization;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's checks of building types: levels 1..max, what houses and producers must have and must not have, and no level with fewer
    /// workers than the one below it. An upgrade keeps the assigned workers, so a smaller max would break <c>assigned ≤ MaxWorkers</c>, and a
    /// smaller house would break <c>Σ assigned ≤ provided</c> (<c>core-instructions/04-simulation-model.md</c>, invariant 9).
    /// </summary>
    internal static class BuildingRules
    {
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            foreach (BuildingDefinition building in draft.Buildings)
            {
                CheckLevelNumbers(building, errors);
                foreach (BuildingLevelDefinition level in building.Levels)
                {
                    if (building.Kind == BuildingKind.House)
                    {
                        CheckHouseLevel(building, level, errors);
                    }
                    else
                    {
                        CheckProducerLevel(building, level, errors);
                    }
                }

                CheckWorkersNeverDrop(building, errors);
            }
        }

        private static void CheckLevelNumbers(BuildingDefinition building, ContentErrorList errors)
        {
            // Levels are sorted by number, so 1..max means exactly: the level at position i has the number i + 1.
            bool contiguous = building.Levels.Count > 0 && building.Levels.Select((level, index) => level.Level == index + 1).All(isNext => isNext);
            if (!contiguous)
            {
                string numbers = string.Join(", ", building.Levels.Select(level => level.Level.ToString(CultureInfo.InvariantCulture)));
                errors.Add(
                    ContentErrorCode.LevelsNotContiguous,
                    ContentPaths.Building(building) + ".levels",
                    "The levels must be numbered 1, 2, … up to the max level, each once, but are: " + (numbers.Length == 0 ? "none" : numbers) + ".");
            }
        }

        private static void CheckHouseLevel(BuildingDefinition building, BuildingLevelDefinition level, ContentErrorList errors)
        {
            string path = ContentPaths.Level(building, level);
            if (level.WorkersProvided < 1)
            {
                errors.Add(ContentErrorCode.InvalidHouseLevel, path + ".workersProvided", "A house level provides at least 1 worker.");
            }

            if (level.MaxWorkers != 0)
            {
                errors.Add(ContentErrorCode.InvalidHouseLevel, path + ".maxWorkers", "A house takes no workers; leave maxWorkers out.");
            }

            if (level.Recipe is not null)
            {
                errors.Add(ContentErrorCode.InvalidHouseLevel, path + ".recipe", "A house has no recipe.");
            }
        }

        private static void CheckProducerLevel(BuildingDefinition building, BuildingLevelDefinition level, ContentErrorList errors)
        {
            string path = ContentPaths.Level(building, level);
            if (level.MaxWorkers < 1)
            {
                errors.Add(ContentErrorCode.InvalidProducerLevel, path + ".maxWorkers", "A producer level has at least 1 worker slot.");
            }

            if (level.WorkersProvided != 0)
            {
                errors.Add(ContentErrorCode.InvalidProducerLevel, path + ".workersProvided", "Only houses provide workers; leave workersProvided out.");
            }

            if (level.Recipe is null)
            {
                errors.Add(ContentErrorCode.InvalidProducerLevel, path + ".recipe", "A producer level has a recipe.");
            }
            else if (level.Recipe.Outputs.Count == 0)
            {
                errors.Add(ContentErrorCode.InvalidProducerLevel, path + ".recipe.outputs", "A recipe makes at least one item.");
            }
        }

        private static void CheckWorkersNeverDrop(BuildingDefinition building, ContentErrorList errors)
        {
            // Only the number that matters for the kind is compared; the other one is 0 by the checks above, or already reported. A count
            // below 1 is already reported as an invalid level, so it is not compared again.
            Func<BuildingLevelDefinition, int> workers = building.Kind == BuildingKind.House
                ? level => level.WorkersProvided
                : level => level.MaxWorkers;
            string field = building.Kind == BuildingKind.House ? ".workersProvided" : ".maxWorkers";
            for (int index = 1; index < building.Levels.Count; index++)
            {
                BuildingLevelDefinition below = building.Levels[index - 1];
                BuildingLevelDefinition level = building.Levels[index];
                if (workers(below) >= 1 && workers(level) >= 1 && workers(level) < workers(below))
                {
                    errors.Add(
                        ContentErrorCode.WorkersDecrease,
                        ContentPaths.Level(building, level) + field,
                        FormattableString.Invariant(
                            $"Level {level.Level} has {workers(level)}, fewer than the {workers(below)} of level {below.Level}; an upgrade keeps the assigned workers."));
                }
            }
        }
    }
}
