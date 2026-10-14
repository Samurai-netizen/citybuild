#nullable enable
using System;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's labor checks. The labor reductions of one building type, over all research, add up to at most
    /// <see cref="GameRules.MaxLaborReductionPermille"/>. With all of them applied and all max workers of a level assigned, a cycle at that
    /// level takes at least <see cref="GameRules.MinCycleSeconds"/>, checked on every level with its own recipe and max workers. Values
    /// already reported (labor ≤ 0, max workers below 1, permille outside 1..1000, an invalid limit) are left out.
    /// </summary>
    internal static class LaborRules
    {
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            GameRules rules = draft.Rules;
            bool limitIsValid = rules.MaxLaborReductionPermille >= 0 && rules.MaxLaborReductionPermille <= IntegerMath.PermilleScale;
            foreach (BuildingDefinition building in draft.Buildings.Where(draft.IsFirst).Where(building => building.Kind == BuildingKind.Producer))
            {
                long reduction = TotalReduction(draft, building.Id);
                if (limitIsValid && reduction > rules.MaxLaborReductionPermille)
                {
                    errors.Add(
                        ContentErrorCode.LaborReductionTooHigh,
                        ContentPaths.Building(building),
                        FormattableString.Invariant(
                            $"The labor reductions of {building.Id.Value} add up to {reduction} permille, more than maxLaborReductionPermille ({rules.MaxLaborReductionPermille})."));
                }

                // A sum above the limit is reported just above; the cycle check then asks whether even the allowed reduction is too much.
                long applied = Math.Min(reduction, limitIsValid ? rules.MaxLaborReductionPermille : IntegerMath.PermilleScale);
                CheckCycles(building, (int)applied, rules.MinCycleSeconds, errors);
            }
        }

        private static long TotalReduction(ContentDraft draft, BuildingTypeId building)
        {
            // A long sum of values of at most 1000 each cannot overflow. Out-of-range values are reported by the value rules.
            long total = 0;
            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                foreach (LaborReductionEffect effect in node.Effects.OfType<LaborReductionEffect>())
                {
                    if (effect.Building == building && effect.Permille >= 1 && effect.Permille <= IntegerMath.PermilleScale)
                    {
                        total = checked(total + effect.Permille);
                    }
                }
            }

            return total;
        }

        private static void CheckCycles(BuildingDefinition building, int reductionPermille, long minCycleSeconds, ContentErrorList errors)
        {
            foreach (BuildingLevelDefinition level in building.Levels)
            {
                if (level.Recipe is null || level.Recipe.Labor <= 0 || level.MaxWorkers < 1 || minCycleSeconds <= 0)
                {
                    continue;
                }

                long labor = IntegerMath.ApplyPermilleReduction(level.Recipe.Labor, reductionPermille);
                long shortestCycle = IntegerMath.CeilDiv(labor, level.MaxWorkers);
                if (shortestCycle < minCycleSeconds)
                {
                    errors.Add(
                        ContentErrorCode.CycleTooShort,
                        ContentPaths.Level(building, level),
                        FormattableString.Invariant(
                            $"With every labor reduction ({reductionPermille} permille: {level.Recipe.Labor} → {labor} worker-seconds) and {level.MaxWorkers} workers a cycle takes {shortestCycle} s, less than minCycleSeconds ({minCycleSeconds})."));
                }
            }
        }
    }
}
