using CityBuilder.Api.Errors;

namespace CityBuilder.DocTools.Pages;

/// <summary>
/// <c>errors.md</c>: the error catalogue as the server answers with it (<see cref="ErrorCatalogue.All"/>): code, HTTP status, title,
/// meaning and Problem Details type, in the order of the contract's table in <c>core-instructions/05-api-contract.md</c>.
/// </summary>
public static class ErrorsPage
{
    /// <summary>The page's file name.</summary>
    public const string FileName = "errors.md";

    /// <summary>Generates the page.</summary>
    /// <returns>The page.</returns>
    public static GeneratedPage Generate()
    {
        var page = new MarkdownPage("Error codes", "`ErrorCatalogue` in `server/src/CityBuilder.Api/Errors/ErrorCatalogue.cs`");
        page.Paragraph("Every error is Problem Details with the code in its `code` extension (`core-instructions/05-api-contract.md` → Errors). " +
                       "The meaning is the `detail` the server sends unless a rule gives a more precise one.")
            .Table(["Code", "HTTP status", "Title", "Meaning", "Type"], ErrorCatalogue.All.Select(error => (IReadOnlyList<string>)
            [
                MarkdownPage.Code(error.Code), PageFormat.Number(error.Status), MarkdownPage.Text(error.Title), MarkdownPage.Text(error.Detail), MarkdownPage.Code(error.Type),
            ]));
        return page.ToPage(FileName);
    }
}
