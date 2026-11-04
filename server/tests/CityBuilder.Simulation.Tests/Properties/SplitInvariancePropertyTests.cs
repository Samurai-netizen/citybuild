using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Property 2, split invariance (<c>04</c> section 10, invariant 2): for one <c>absentFrom</c>, advancing a city by a and then by b gives the
/// same city as advancing it by a + b, for any lengths, and the two reports add up to the one report.
/// </summary>
public sealed class SplitInvariancePropertyTests
{
    /// <summary>
    /// For every seed, a random total cut into two pieces (sometimes all of it in the first or the second), with the player present or absent
    /// from a random second (before the window, inside it or after it), gives the same city and report totals as one advance over the total.
    /// </summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Advance_TwoPiecesWithOneAbsence_EqualsOneAdvanceOverBoth(string contentName)
    {
        GameContent content = PropertyContent.Named(contentName);
        PropertyCheck.ForEachSeed("split invariance", contentName, (history, random) =>
        {
            long total = Total(random, CityGenerator.LongGapOf(content));
            long start = history.City.Clock;
            long? absentFrom = random.Chance(25) ? null : random.Between(start - CityGenerator.MaxGraceSeconds, start + total);
            long first = random.Between(1, 10) switch
            {
                1 => 0,
                2 => total,
                _ => random.Between(0, total),
            };
            long second = total - first;

            CityState whole = history.Copy(content);
            ReportTotals wholeReport = ReportTotals.Sum(Simulator.Advance(whole, content, total, absentFrom));
            CityState split = history.Copy(content);
            ReportTotals splitReports = ReportTotals.Sum(
                Simulator.Advance(split, content, first, absentFrom), Simulator.Advance(split, content, second, absentFrom));

            string? difference = StructuralEquality.FirstDifference(whole, split) ?? StructuralEquality.FirstDifference(wholeReport, splitReports);
            return difference is null ? null
                : $"advance {first} then {second} against {total} at clock {start}, absent from {absentFrom?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "never"}: {difference}";
        });
    }

    /// <summary>
    /// The total seconds, a quarter each: up to a minute, up to ten minutes, up to the content's long gap (<see cref="CityGenerator.LongGapOf"/>),
    /// and up to four of them.
    /// </summary>
    private static long Total(SeededRandom random, long longGap) => random.Between(1, 4) switch
    {
        1 => random.Between(0, 60),
        2 => random.Between(0, 600),
        3 => random.Between(0, longGap),
        _ => random.Between(0, 4 * longGap),
    };
}
