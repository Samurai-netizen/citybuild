namespace CityBuilder.Api.Http;

/// <summary>
/// The limits on what a request may send (core-instructions/07-security-model.md, huge bodies): the configuration section
/// <c>RequestLimits</c>. Validated on start: a value out of range stops it.
/// </summary>
public sealed class RequestLimitOptions
{
    /// <summary>The configuration section's name.</summary>
    public const string SectionName = "RequestLimits";

    /// <summary>The lowest allowed <see cref="MaxBodyBytes"/>: room for any body of the contract, a long display name or id included.</summary>
    public const int MinMaxBodyBytes = 1024;

    /// <summary>The highest allowed <see cref="MaxBodyBytes"/>: a mebibyte, far above any body the contract has.</summary>
    public const int MaxMaxBodyBytes = 1_048_576;

    /// <summary>
    /// <c>RequestLimits:MaxBodyBytes</c>: the largest request body the server reads, in bytes. A body of the contract is a few hundred
    /// bytes; a larger one is refused before anything reads or runs it (<see cref="RequestBodyLimit"/>). Default 4096; between
    /// <see cref="MinMaxBodyBytes"/> and <see cref="MaxMaxBodyBytes"/>, or the start stops.
    /// </summary>
    public int MaxBodyBytes { get; set; } = 4096;
}
