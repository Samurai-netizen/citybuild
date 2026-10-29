namespace CityBuilder.Api.Errors;

/// <summary>
/// One error as the client sees it (core-instructions/05-api-contract.md, errors): the stable code programs branch on, its HTTP status, and
/// a title and detail for people, which may change. The members of <see cref="ErrorCatalogue"/> are the only ones that exist; a writer
/// that has something more to say about one case (what exactly was wrong) makes a copy with <see cref="WithDetail"/>.
/// </summary>
/// <param name="Code">The stable code, for example <c>TILE_OCCUPIED</c>.</param>
/// <param name="Status">The HTTP status of the code in <c>05</c>.</param>
/// <param name="Title">A short summary for people.</param>
/// <param name="Detail">What went wrong, for people.</param>
public sealed record ApiError(string Code, int Status, string Title, string Detail)
{
    /// <summary>The Problem Details <c>type</c> of the code: <c>urn:citybuilder:error:</c> and the code in lower case with hyphens.</summary>
    public string Type => "urn:citybuilder:error:" + Code.Replace('_', '-').ToLowerInvariant();

    /// <summary>The same error with another detail, for a case the catalogue's own text doesn't describe.</summary>
    /// <param name="detail">What is wrong in this case.</param>
    /// <returns>The copy; the code, status and title are unchanged.</returns>
    public ApiError WithDetail(string detail) => this with { Detail = detail };
}
