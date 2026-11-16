using System.Text;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Output;

/// <summary>
/// The markdown files of a run (docs/modules/BalanceSim.md → Output): <c>summary.md</c> with the metrics of core-instructions/
/// 24-economy-design.md → Metrics we track, and <c>index.md</c> with one row per run. Times are city-clock <c>H:MM:SS</c> from the first
/// check-in; "—" means it never happened in the run.
/// </summary>
public static class SummaryMarkdown
{
    /// <summary>A run's <c>summary.md</c>.</summary>
    /// <param name="result">The run.</param>
    /// <returns>The markdown.</returns>
    public static string Write(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        RunOptions options = result.Options;
        RunRecorder recorder = result.Recorder;
        GameContent content = result.Content;
        var text = new StringBuilder();
        Line(text, $"# Balance run: {options.Strategy}, \"{options.Sessions.Text}\"");
        Line(text);
        Table(text, ["Setting", "Value"],
        [
            ["Content version", content.ContentVersion],
            ["Strategy", options.Strategy.ToString()],
            ["Check-ins", $"\"{options.Sessions.Text}\": {result.Sessions.Count} in the run"],
            ["Run length", Format.Clock(options.RunSeconds)],
            ["First check-in / later check-ins", $"{options.FirstSessionSeconds / 60} min / {options.SessionSeconds / 60} min"],
            ["Absent from", $"{options.GraceSeconds} s after a check-in ends"],
            ["Decision step / absent step / timeline rows", $"{RunOptions.PresentTickSeconds} s / {RunOptions.AbsentTickSeconds} s / every {options.SampleSeconds / 60} min"],
        ]);

        Line(text);
        Line(text, "## Milestones");
        Line(text);
        Table(text, ["Milestone", "Time"], Milestones(result));

        Line(text);
        Line(text, "## Coins");
        Line(text);
        var coins = new List<string[]>
        {
            new[] { "Produced", Format.Number(recorder.CoinsProduced) },
            new[] { "Per hour over the run", Format.Number(Format.PerHour(recorder.CoinsProduced, options.RunSeconds)) },
        };
        coins.AddRange(recorder.CoinsPerDay.Select(day => new[] { $"Produced on day {day.Key + 1}", Format.Number(day.Value) }));
        coins.Add(["Spent on placements", Format.Number(recorder.SpentOn(SpendCategory.Placement))]);
        coins.Add(["Spent on upgrades", Format.Number(recorder.SpentOn(SpendCategory.Upgrade))]);
        coins.Add(["Spent on research", Format.Number(recorder.SpentOn(SpendCategory.Research))]);
        coins.Add(["Purse at the end", Format.Number(result.City.Purse)]);
        Table(text, ["Coins", "Value"], coins);

        Line(text);
        Line(text, "## Goods");
        Line(text);
        Table(text, ["Good", "Produced", "Used by recipes", "Delivered to projects", "Put into stores", "In stores at the end"],
            content.Items.Where(item => !item.IsCurrency).Select(item => new[]
            {
                item.Id.Value,
                Format.Number(recorder.Produced.GetValueOrDefault(item.Id.Value)),
                Format.Number(recorder.Consumed.GetValueOrDefault(item.Id.Value)),
                Format.Number(recorder.Delivered.GetValueOrDefault(item.Id.Value)),
                Format.Number(recorder.Moved.GetValueOrDefault(item.Id.Value)),
                Format.Number(CityQueries.Total(result.City, content, item.Id)),
            }));

        Line(text);
        Line(text, "## Workers");
        Line(text);
        Table(text, ["Workers", "Value"],
        [
            ["Provided, on average", Format.Average(recorder.ProvidedWorkerSeconds, recorder.MeasuredSeconds)],
            ["Free (idle), on average", Format.Average(recorder.IdleWorkerSeconds, recorder.MeasuredSeconds)],
            ["Share of worker time idle", Format.Percent(recorder.IdleWorkerSeconds, recorder.ProvidedWorkerSeconds)],
            ["Share of time with at least one idle worker", Format.Percent(recorder.SecondsWithIdleWorkers, recorder.MeasuredSeconds)],
            ["Provided / assigned / free at the end", $"{CityQueries.WorkersProvided(result.City, content)} / {CityQueries.WorkersAssigned(result.City)} / {CityQueries.FreeWorkers(result.City, content)}"],
        ]);

        Line(text);
        Line(text, "## Producer time by state");
        Line(text);
        Line(text, "Share of each producer type's time (from its completion) running, upgrading or stalled, by stall reason (`04` §4).");
        Line(text);
        WriteStates(text, recorder);

        Line(text);
        Line(text, "## Logistics");
        Line(text);
        Table(text, ["Logistics", "Value"],
        [
            ["Courier stamina spent (seconds walked while absent, all couriers)", Format.Number(recorder.StaminaSpent)],
            ["Times a courier ran out of stamina", Format.Number(recorder.TiredCouriers)],
            ["Couriers at the end", Format.Number(result.City.Couriers.Count)],
            ["Routes at the end", Format.Number(result.City.Routes.Count)],
        ]);

        Line(text);
        Line(text, "## City at the end");
        Line(text);
        Table(text, ["Building type", "Count", "Levels"],
            content.Buildings.Select(type => (type, buildings: result.City.Buildings.Where(building => building.TypeId == type.Id).ToArray()))
                .Where(entry => entry.buildings.Length > 0)
                .Select(entry => new[]
                {
                    entry.type.Id.Value, Format.Number(entry.buildings.Length),
                    string.Join(" ", entry.buildings.Select(building => "L" + Format.Number(building.Level))),
                }));
        Line(text);
        Line(text, "Research done: " + (result.City.CompletedResearch.Count == 0
            ? "none."
            : string.Join(", ", result.City.CompletedResearch.Select(id => "`" + id.Value + "`")) + "."));

        Line(text);
        Line(text, "## Rejected actions");
        Line(text);
        if (recorder.Rejections.Count == 0)
        {
            Line(text, "None: every action the strategy tried applied.");
        }
        else
        {
            Table(text, ["Action: error", "Count"], recorder.Rejections.Select(pair => new[] { pair.Key, Format.Number(pair.Value) }));
        }

        return text.ToString();
    }

    /// <summary>The <c>index.md</c> of several runs: one row each, in run order, with the headline metrics and a link to its summary.</summary>
    /// <param name="results">The runs.</param>
    /// <returns>The markdown.</returns>
    public static string WriteIndex(IReadOnlyList<RunResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var text = new StringBuilder();
        Line(text, "# Balance runs");
        Line(text);
        if (results.Count > 0)
        {
            RunResult first = results[0];
            Line(text, $"Content `{first.Content.ContentVersion}`, {Format.Clock(first.Options.RunSeconds)} per run. Times are from the first check-in.");
            Line(text);
        }

        Table(text, ["Strategy", "Check-ins", "First research", "First coins", "All research", "Coins/h", "Purse at end", "Idle worker time", "Top stall", "Run"],
            results.Select(result => new[]
            {
                result.Options.Strategy.ToString(),
                "\"" + result.Options.Sessions.Text + "\"",
                Format.Clock(result.Recorder.ResearchStarts.Count > 0 ? result.Recorder.ResearchStarts[0].At : null),
                Format.Clock(result.Recorder.FirstCoinsAt),
                Format.Clock(AllResearchAt(result)),
                Format.Number(Format.PerHour(result.Recorder.CoinsProduced, result.Options.RunSeconds)),
                Format.Number(result.City.Purse),
                Format.Percent(result.Recorder.IdleWorkerSeconds, result.Recorder.ProvidedWorkerSeconds),
                TopStall(result.Recorder),
                $"[summary]({RunWriter.FolderName(result.Options)}/summary.md)",
            }));
        return text.ToString();
    }

    /// <summary>The milestone rows: first research, first coins, each research start and completion, all research, each building type, all types at max level.</summary>
    /// <param name="result">The run.</param>
    /// <returns>The rows.</returns>
    private static List<string[]> Milestones(RunResult result)
    {
        RunRecorder recorder = result.Recorder;
        var rows = new List<string[]>
        {
            new[] { "First research started", Format.Clock(recorder.ResearchStarts.Count > 0 ? recorder.ResearchStarts[0].At : null) },
            new[] { "First coins (at the step resolution)", Format.Clock(recorder.FirstCoinsAt) },
        };
        foreach (ResearchDefinition node in result.Content.Research.Where(node => node.Era == result.City.Era))
        {
            long? started = recorder.ResearchStarts.Where(start => start.Research == node.Id).Select(start => (long?)start.At).FirstOrDefault();
            long? done = recorder.ResearchDone.Where(end => end.Research == node.Id).Select(end => (long?)end.At).FirstOrDefault();
            rows.Add([$"Research `{node.Id.Value}` started / done", $"{Format.Clock(started)} / {Format.Clock(done)}"]);
        }

        rows.Add(["All research done", Format.Clock(AllResearchAt(result))]);
        foreach (BuildingDefinition type in result.Content.Buildings)
        {
            (long? built, long? max) = recorder.BuildingMilestones.GetValueOrDefault(type.Id.Value);
            rows.Add([$"First `{type.Id.Value}` built / at max level {type.MaxLevel}", $"{Format.Clock(built)} / {Format.Clock(max)}"]);
        }

        long?[] maxed = [.. result.Content.Buildings.Where(type => type.Era <= result.City.Era)
            .Select(type => recorder.BuildingMilestones.GetValueOrDefault(type.Id.Value).FirstMax)];
        rows.Add(["Every building type at its max level", Format.Clock(maxed.All(at => at.HasValue) ? maxed.Max() : null)]);
        return rows;
    }

    /// <summary>The second every research node of the city's era was done, or null when one wasn't.</summary>
    /// <param name="result">The run.</param>
    /// <returns>The second, or null.</returns>
    private static long? AllResearchAt(RunResult result)
    {
        ResearchId[] nodes = [.. result.Content.Research.Where(node => node.Era == result.City.Era).Select(node => node.Id)];
        long?[] done = [.. nodes.Select(id => result.Recorder.ResearchDone.Where(end => end.Research == id).Select(end => (long?)end.At).FirstOrDefault())];
        return nodes.Length > 0 && done.All(at => at.HasValue) ? done.Max() : null;
    }

    /// <summary>The producer-state table: one row per producer type, one column per state seen, plus an "all producers" row.</summary>
    /// <param name="text">Where the table goes.</param>
    /// <param name="recorder">The run's recorder.</param>
    private static void WriteStates(StringBuilder text, RunRecorder recorder)
    {
        string[] states = [.. recorder.ProducerSeconds.Values.SelectMany(byState => byState.Keys).Distinct().Order(StringComparer.Ordinal)];
        if (states.Length == 0)
        {
            Line(text, "No producer was completed.");
            return;
        }

        var totals = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var rows = new List<string[]>();
        foreach ((string type, SortedDictionary<string, long> byState) in recorder.ProducerSeconds)
        {
            long whole = byState.Values.Sum();
            rows.Add([type, .. states.Select(state => Format.Percent(byState.GetValueOrDefault(state), whole))]);
            foreach ((string state, long seconds) in byState)
            {
                totals[state] = totals.GetValueOrDefault(state) + seconds;
            }
        }

        long all = totals.Values.Sum();
        rows.Add(["all producers", .. states.Select(state => Format.Percent(totals.GetValueOrDefault(state), all))]);
        Table(text, ["Producer type", .. states.Select(state => "`" + state + "`")], rows);
    }

    /// <summary>The stall reason with the most producer time, with its share, or "—" when nothing stalled.</summary>
    /// <param name="recorder">The run's recorder.</param>
    /// <returns>The text.</returns>
    private static string TopStall(RunRecorder recorder)
    {
        var totals = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (SortedDictionary<string, long> byState in recorder.ProducerSeconds.Values)
        {
            foreach ((string state, long seconds) in byState)
            {
                totals[state] = totals.GetValueOrDefault(state) + seconds;
            }
        }

        long all = totals.Values.Sum();
        KeyValuePair<string, long>[] stalls = [.. totals.Where(pair => pair.Key is not ("Running" or "Upgrading"))
            .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal)];
        return stalls.Length == 0 ? "—" : $"`{stalls[0].Key}` {Format.Percent(stalls[0].Value, all)}";
    }

    /// <summary>Writes a markdown table.</summary>
    /// <param name="text">Where it goes.</param>
    /// <param name="header">The column names.</param>
    /// <param name="rows">The rows, each with one value per column.</param>
    private static void Table(StringBuilder text, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        Line(text, "| " + string.Join(" | ", header) + " |");
        Line(text, "|" + string.Concat(header.Select(_ => "---|")));
        foreach (IReadOnlyList<string> row in rows)
        {
            Line(text, "| " + string.Join(" | ", row) + " |");
        }
    }

    /// <summary>Writes a line with a <c>\n</c> end (never the platform's line end, so every machine writes the same bytes).</summary>
    /// <param name="text">Where it goes.</param>
    /// <param name="line">The line; empty for a blank line.</param>
    private static void Line(StringBuilder text, string line = "") => text.Append(line).Append('\n');
}
