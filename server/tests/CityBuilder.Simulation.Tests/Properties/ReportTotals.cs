using CityBuilder.Simulation.Settlement;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The sum of one or more settlement reports, so the reports of an advance split into pieces can be compared with the report of one advance
/// over the whole time: seconds and quantities add up, the completed buildings and research of all pieces are listed together (sorted, as one
/// report lists them), and the era changed if it changed in any piece.
/// </summary>
internal sealed class ReportTotals
{
    private readonly long _elapsedSeconds;
    private readonly long _productionSeconds;
    private readonly SortedDictionary<ItemId, long> _produced = [];
    private readonly SortedDictionary<ItemId, long> _consumed = [];
    private readonly List<int> _completedBuildings = [];
    private readonly List<ResearchId> _completedResearch = [];
    private readonly bool _eraChanged;

    private ReportTotals(IEnumerable<SettlementReport> reports)
    {
        foreach (SettlementReport report in reports)
        {
            _elapsedSeconds = checked(_elapsedSeconds + report.ElapsedSeconds);
            _productionSeconds = checked(_productionSeconds + report.ProductionSeconds);
            Add(_produced, report.Produced);
            Add(_consumed, report.Consumed);
            _completedBuildings.AddRange(report.CompletedBuildings);
            _completedResearch.AddRange(report.CompletedResearch);
            _eraChanged |= report.EraChanged;
        }

        _completedBuildings.Sort();
        _completedResearch.Sort();
    }

    /// <summary>The totals of the reports, in any number (none gives zeros and empty lists).</summary>
    public static ReportTotals Sum(params IEnumerable<SettlementReport> reports) => new(reports);

    /// <summary>Every value, as a path and a value: seconds, the quantities per item, the completions, the era flag.</summary>
    public IReadOnlyList<(string Path, string Value)> Fields()
    {
        var fields = new List<(string Path, string Value)>
        {
            ("elapsedSeconds", StructuralEquality.Text(_elapsedSeconds)),
            ("productionSeconds", StructuralEquality.Text(_productionSeconds)),
        };
        fields.AddRange(_produced.Select(item => ($"produced[{item.Key}]", StructuralEquality.Text(item.Value))));
        fields.AddRange(_consumed.Select(item => ($"consumed[{item.Key}]", StructuralEquality.Text(item.Value))));
        fields.Add(("completedBuildings", string.Join(",", _completedBuildings)));
        fields.Add(("completedResearch", string.Join(",", _completedResearch)));
        fields.Add(("eraChanged", _eraChanged ? "true" : "false"));
        return fields;
    }

    private static void Add(SortedDictionary<ItemId, long> totals, IReadOnlyDictionary<ItemId, long> quantities)
    {
        foreach (KeyValuePair<ItemId, long> quantity in quantities)
        {
            totals[quantity.Key] = checked(totals.GetValueOrDefault(quantity.Key) + quantity.Value);
        }
    }
}
