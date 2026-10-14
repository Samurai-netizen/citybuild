using System.Globalization;

namespace CityBuilder.Infrastructure.Content;

/// <summary>
/// A content file could not be loaded. The message names the file and lists every error on its own line, so the server's startup
/// failure says everything that needs fixing at once.
/// </summary>
public sealed class ContentLoadException : Exception
{
    /// <summary>Creates the exception for a file and its errors.</summary>
    /// <param name="filePath">The content file.</param>
    /// <param name="errors">Every error found, at least one, each on one line.</param>
    /// <param name="innerException">The exception that caused the error, if any.</param>
    public ContentLoadException(string filePath, IReadOnlyList<string> errors, Exception? innerException = null)
        : base(FormatMessage(filePath, errors), innerException)
    {
        FilePath = filePath;
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    /// <summary>The content file.</summary>
    public string FilePath { get; }

    /// <summary>Every error found, each on one line: for a rule error, its code, path and message (<c>ContentError.ToString</c>).</summary>
    public IReadOnlyList<string> Errors { get; }

    private static string FormatMessage(string filePath, IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A content load fails with at least one error.", nameof(errors));
        }

        // Invariant culture, like the engine's error messages, so the startup report reads the same on every machine.
        string count = errors.Count == 1 ? "1 error" : string.Create(CultureInfo.InvariantCulture, $"{errors.Count} errors");
        return $"The content file '{filePath}' can't be loaded ({count}):" + string.Concat(errors.Select(error => Environment.NewLine + "  - " + error));
    }
}
