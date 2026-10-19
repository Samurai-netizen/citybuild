using System.Globalization;
using System.Text;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// A city and the operations that made it from the starting city: only those the rules accepted, each with its report. Replaying the
/// operations on a new starting city gives the same city again, so a property can make as many equal copies as it needs.
/// </summary>
internal sealed class CityHistory
{
    public CityHistory(int seed, CityState city, IReadOnlyList<CityOperation> operations, IReadOnlyList<SettlementReport> reports, int rejected)
    {
        Seed = seed;
        City = city;
        Operations = operations;
        Reports = reports;
        Rejected = rejected;
    }

    /// <summary>The seed the history was generated from.</summary>
    public int Seed { get; }

    /// <summary>The city after the last operation.</summary>
    public CityState City { get; }

    /// <summary>The accepted operations, in order.</summary>
    public IReadOnlyList<CityOperation> Operations { get; }

    /// <summary>The report of each accepted operation, in the same order.</summary>
    public IReadOnlyList<SettlementReport> Reports { get; }

    /// <summary>How many generated commands the rules rejected (they changed nothing and are not in <see cref="Operations"/>).</summary>
    public int Rejected { get; }

    /// <summary>Runs <paramref name="operations"/> on a new starting city of <paramref name="content"/>; every one must be accepted.</summary>
    /// <exception cref="InvalidOperationException">The rules rejected an operation that was accepted when the history was made.</exception>
    public static CityHistory Replay(GameContent content, int seed, IReadOnlyList<CityOperation> operations)
    {
        CityState city = CityFactory.CreateStarterCity(content);
        var reports = new List<SettlementReport>(operations.Count);
        for (int index = 0; index < operations.Count; index++)
        {
            SettlementReport report = operations[index].TryApply(city, content)
                ?? throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture, $"Operation {index + 1} ({operations[index]}) was rejected on replay."));
            reports.Add(report);
        }

        return new CityHistory(seed, city, operations, reports, rejected: 0);
    }

    /// <summary>A new copy of the city: the operations replayed on a new starting city.</summary>
    public CityState Copy(GameContent content) => Replay(content, Seed, Operations).City;

    /// <summary>The seed and the numbered operation list, for a failure message.</summary>
    public string Describe()
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"seed {Seed}, {Operations.Count} operations from the starting city:");
        for (int index = 0; index < Operations.Count; index++)
        {
            text.Append(CultureInfo.InvariantCulture, $"\n  {index + 1}. {Operations[index]}");
        }

        return text.ToString();
    }
}
