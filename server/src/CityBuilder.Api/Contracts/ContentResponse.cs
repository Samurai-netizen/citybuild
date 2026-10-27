namespace CityBuilder.Api.Contracts;

/// <summary>
/// The answer of <c>GET /api/v1/content</c> (core-instructions/05-api-contract.md): the game content the server runs on, the same for every
/// player until the server restarts with new content. The client fetches it once and again when a snapshot's <c>contentVersion</c> differs.
/// Every list is in a fixed order (eras by number, everything else by id in ordinal order), so the text is the same on every call.
/// </summary>
/// <param name="ContentVersion">The content's version, JSON <c>contentVersion</c>, for example <c>mvp-1</c>.</param>
/// <param name="Rules">The rules that apply to every city, JSON <c>rules</c>.</param>
/// <param name="Eras">The eras, JSON <c>eras</c>, by ascending number.</param>
/// <param name="Items">The items, JSON <c>items</c>, by id.</param>
/// <param name="Buildings">The building types, JSON <c>buildings</c>, by id.</param>
/// <param name="Research">The research nodes, JSON <c>research</c>, by id.</param>
public sealed record ContentResponse(
    string ContentVersion,
    ContentRules Rules,
    IReadOnlyList<ContentEra> Eras,
    IReadOnlyList<ContentItem> Items,
    IReadOnlyList<ContentBuilding> Buildings,
    IReadOnlyList<ContentResearch> Research);
