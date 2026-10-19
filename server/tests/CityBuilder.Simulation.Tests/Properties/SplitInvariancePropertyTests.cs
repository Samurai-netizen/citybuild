using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Property 2, split invariance (<c>04</c> section 10, invariant 2): for a + b within the production cap, advancing a city by a and then by b
/// gives the same city as advancing it by a + b, and the two reports add up to the one report.
/// </summary>
public sealed class SplitInvariancePropertyTests
{
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Advance_TwoPiecesWithinTheCap_EqualsOneAdvanceOverBoth(string contentName)
    {
        GameContent content = PropertyContent.Named(contentName);
        PropertyCheck.ForEachSeed("split invariance", contentName, (history, random) =>
        {
            long total = Total(random, content.Rules.MaxOfflineProductionSeconds);
            long first = random.Between(1, 10) switch
            {
                1 => 0,
                2 => total,
                _ => random.Between(0, total),
            };
            long second = total - first;

            CityState whole = history.Copy(content);
            ReportTotals wholeReport = ReportTotals.Sum(Simulator.Advance(whole, content, total));
            CityState split = history.Copy(content);
            ReportTotals splitReports = ReportTotals.Sum(Simulator.Advance(split, content, first), Simulator.Advance(split, content, second));

            string? difference = StructuralEquality.FirstDifference(whole, split) ?? StructuralEquality.FirstDifference(wholeReport, splitReports);
            return difference is null ? null : $"advance {first} then {second} against {total} at clock {history.City.Clock}: {difference}";
        });
    }

    // A quarter each: up to a minute, up to ten minutes, anywhere up to the cap, and exactly the cap (the largest a + b allowed).
    private static long Total(SeededRandom random, long cap) => random.Between(1, 4) switch
    {
        1 => random.Between(0, Math.Min(60, cap)),
        2 => random.Between(0, Math.Min(600, cap)),
        3 => random.Between(0, cap),
        _ => cap,
    };
}
