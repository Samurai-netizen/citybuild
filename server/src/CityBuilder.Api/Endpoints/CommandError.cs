namespace CityBuilder.Api.Endpoints;

/// <summary>
/// One error of a command as the client sees it (core-instructions/05-api-contract.md, errors): the stable code programs branch on, its
/// HTTP status, and a title and detail for people, which may change.
/// </summary>
/// <param name="Code">The stable code, for example <c>TILE_OCCUPIED</c>.</param>
/// <param name="Status">The HTTP status of the code in <c>05</c>.</param>
/// <param name="Title">A short summary for people.</param>
/// <param name="Detail">What went wrong, for people.</param>
public sealed record CommandError(string Code, int Status, string Title, string Detail)
{
    /// <summary>The Problem Details <c>type</c> of the code: <c>urn:citybuilder:error:</c> and the code in lower case with hyphens.</summary>
    public string Type => "urn:citybuilder:error:" + Code.Replace('_', '-').ToLowerInvariant();
}
