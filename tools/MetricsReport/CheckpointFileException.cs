namespace CityBuilder.MetricsReport;

/// <summary>The checkpoint file can't be read or is broken; the message names the file and the line, and the report stops (exit code 1).</summary>
public sealed class CheckpointFileException : Exception
{
    /// <summary>Makes the error.</summary>
    /// <param name="file">The file's path or name.</param>
    /// <param name="line">The 1-based line of the problem, or 0 when the problem is the whole file.</param>
    /// <param name="problem">What is wrong.</param>
    /// <param name="inner">The error that caused it, if any.</param>
    public CheckpointFileException(string file, int line, string problem, Exception? inner = null)
        : base(line > 0 ? $"{file}, line {line}: {problem}" : $"{file}: {problem}", inner)
    {
        Line = line;
    }

    /// <summary>The 1-based line of the problem, or 0 when the problem is the whole file.</summary>
    public int Line { get; }
}
