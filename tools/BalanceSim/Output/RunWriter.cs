using System.Text;
using CityBuilder.Simulation.Content;

namespace CityBuilder.BalanceSim.Output;

/// <summary>
/// Plays a list of runs and writes their files (docs/modules/BalanceSim.md → Output): <c>&lt;strategy&gt;-&lt;pattern&gt;/summary.md</c> and
/// <c>timeline.csv</c> per run, and <c>index.md</c> comparing them. Files are UTF-8 without a byte order mark, with <c>\n</c> line ends, so
/// the same runs write the same bytes on every machine. Other files in the folder are left alone.
/// </summary>
public static class RunWriter
{
    /// <summary>UTF-8 without a byte order mark.</summary>
    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Plays every run, in the order given.</summary>
    /// <param name="content">The game content.</param>
    /// <param name="runs">The runs' settings.</param>
    /// <returns>The results, in the same order.</returns>
    public static IReadOnlyList<RunResult> PlayAll(GameContent content, IReadOnlyList<RunOptions> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        return [.. runs.Select(run => SimulationRun.Play(content, run))];
    }

    /// <summary>A run's folder name: <c>&lt;strategy&gt;-&lt;pattern slug&gt;</c>, for example <c>GreedyCoins-every-4h</c>.</summary>
    /// <param name="options">The run's settings.</param>
    /// <returns>The name.</returns>
    public static string FolderName(RunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Strategy + "-" + options.Sessions.Slug;
    }

    /// <summary>Writes every run's files and the index.</summary>
    /// <param name="folder">The output folder; created if missing.</param>
    /// <param name="results">The results.</param>
    public static void Write(string folder, IReadOnlyList<RunResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        Directory.CreateDirectory(folder);
        foreach (RunResult result in results)
        {
            string runFolder = Path.Combine(folder, FolderName(result.Options));
            Directory.CreateDirectory(runFolder);
            File.WriteAllText(Path.Combine(runFolder, "summary.md"), SummaryMarkdown.Write(result), _utf8);
            File.WriteAllText(Path.Combine(runFolder, "timeline.csv"), Csv(result.Recorder), _utf8);
        }

        File.WriteAllText(Path.Combine(folder, "index.md"), SummaryMarkdown.WriteIndex(results), _utf8);
    }

    /// <summary>The timeline as CSV: the header, then one line per row. No value holds a comma or a quote, so none is quoted.</summary>
    /// <param name="recorder">The run's recorder.</param>
    /// <returns>The CSV text.</returns>
    public static string Csv(RunRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        var text = new StringBuilder();
        text.Append(string.Join(',', recorder.TimelineHeader)).Append('\n');
        foreach (string[] row in recorder.TimelineRows)
        {
            text.Append(string.Join(',', row)).Append('\n');
        }

        return text.ToString();
    }
}
