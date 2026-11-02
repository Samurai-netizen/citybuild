#nullable enable
using System;
using System.Globalization;
using System.Linq;
using CityBuilder.Simulation.Map;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's checks of walking and roads (<c>core-instructions/04-simulation-model.md</c>, sections 14 and 17): the ground time,
    /// the crossing speed and the shortest walk; the road levels are numbered 1, 2, … and each is faster than what it improves.
    /// </summary>
    internal static class WalkRules
    {
        /// <summary>The largest total walk bonus content may allow, in permille (content rule 12): a walk always keeps a tenth of its time.</summary>
        private const int _walkBonusLimitPermille = 900;

        /// <summary>Checks the walking rules and every road level.</summary>
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            GameRules rules = draft.Rules;
            Positive(rules.GroundTileMillis, "rules.groundTileMillis", errors);
            Positive(rules.CrossingSpeedPermille, "rules.crossingSpeedPermille", errors);
            AtMost(rules.CrossingSpeedPermille, IntegerMath.PermilleScale, "rules.crossingSpeedPermille", errors);
            Positive(rules.MinWalkSeconds, "rules.minWalkSeconds", errors);
            AtLeastZero(rules.MaxWalkBonusPermille, "rules.maxWalkBonusPermille", errors);
            AtMost(rules.MaxWalkBonusPermille, _walkBonusLimitPermille, "rules.maxWalkBonusPermille", errors);
            CheckShortestWalk(rules, errors);
            CheckRoadLevelNumbers(draft, errors);
            long slowerTime = rules.GroundTileMillis;
            string slowerName = "the ground time";
            foreach (RoadLevelDefinition level in draft.RoadLevels.OrderBy(level => level.Level))
            {
                string path = ContentPaths.RoadLevel(level);
                Positive(level.TileMillis, path + ".tileMillis", errors);
                foreach (ItemQuantity amount in level.Cost.Where(amount => amount.Quantity < 0))
                {
                    errors.Add(
                        ContentErrorCode.Negative,
                        path + ".cost." + amount.Item.Value,
                        FormattableString.Invariant($"Must be at least 0, but is {amount.Quantity}."));
                }

                if (level.TileMillis >= slowerTime)
                {
                    errors.Add(
                        ContentErrorCode.RoadNotFaster,
                        path + ".tileMillis",
                        FormattableString.Invariant($"A road level must be faster than {slowerName} ({slowerTime} ms per tile), but takes {level.TileMillis} ms."));
                }

                slowerTime = level.TileMillis;
                slowerName = "road level " + level.Level.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// The shortest possible walk (one step between adjacent buildings, every walk bonus applied) takes at least
        /// <see cref="GameRules.MinWalkSeconds"/> (<see cref="ContentErrorCode.WalkTooShort"/>). Nothing is computed when the ground time is not
        /// positive (already reported). The content has no walk bonus yet; the bonus effect arrives with CR-0008-T1 part E.
        /// </summary>
        private static void CheckShortestWalk(GameRules rules, ContentErrorList errors)
        {
            if (rules.GroundTileMillis <= 0)
            {
                return;
            }

            const int everyWalkBonusPermille = 0;
            long shortest = WalkTime.ShortestWalkSeconds(rules.GroundTileMillis, everyWalkBonusPermille);
            if (shortest < rules.MinWalkSeconds)
            {
                errors.Add(
                    ContentErrorCode.WalkTooShort,
                    "rules.groundTileMillis",
                    FormattableString.Invariant(
                        $"One step between adjacent buildings takes {shortest} s with every walk bonus, below minWalkSeconds ({rules.MinWalkSeconds} s)."));
            }
        }

        /// <summary>
        /// Reports road levels that aren't numbered exactly 1, 2, … up to the last one, each number once, or no road level at all: one error
        /// listing the numbers (<see cref="ContentErrorCode.LevelsNotContiguous"/>).
        /// </summary>
        private static void CheckRoadLevelNumbers(ContentDraft draft, ContentErrorList errors)
        {
            int[] numbers = draft.RoadLevels.Select(level => level.Level).OrderBy(number => number).ToArray();
            bool contiguous = numbers.Length > 0 && numbers.Select((number, index) => number == index + 1).All(isNext => isNext);
            if (!contiguous)
            {
                string listed = string.Join(", ", numbers.Select(number => number.ToString(CultureInfo.InvariantCulture)));
                errors.Add(
                    ContentErrorCode.LevelsNotContiguous,
                    "roads",
                    "The road levels must be numbered 1, 2, … up to the last level, each once, but are: " + (listed.Length == 0 ? "none" : listed) + ".");
            }
        }

        /// <summary>Reports a value of 0 or less (<see cref="ContentErrorCode.NotPositive"/>).</summary>
        private static void Positive(long value, string path, ContentErrorList errors)
        {
            if (value <= 0)
            {
                errors.Add(ContentErrorCode.NotPositive, path, FormattableString.Invariant($"Must be greater than 0, but is {value}."));
            }
        }

        /// <summary>Reports a negative value (<see cref="ContentErrorCode.Negative"/>).</summary>
        private static void AtLeastZero(long value, string path, ContentErrorList errors)
        {
            if (value < 0)
            {
                errors.Add(ContentErrorCode.Negative, path, FormattableString.Invariant($"Must be at least 0, but is {value}."));
            }
        }

        /// <summary>Reports a value above <paramref name="highest"/> (<see cref="ContentErrorCode.ValueTooLarge"/>).</summary>
        private static void AtMost(long value, long highest, string path, ContentErrorList errors)
        {
            if (value > highest)
            {
                errors.Add(ContentErrorCode.ValueTooLarge, path, FormattableString.Invariant($"Must be at most {highest}, but is {value}."));
            }
        }
    }
}
