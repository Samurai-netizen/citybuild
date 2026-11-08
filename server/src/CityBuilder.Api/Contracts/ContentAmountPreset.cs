namespace CityBuilder.Api.Contracts;

/// <summary>
/// One amount preset in <see cref="ContentResponse.AmountPresets"/> (<c>core-instructions/04-simulation-model.md</c>, section 19): a share of
/// a base that a trip or a route's action may always use, by its id (CR-0008).
/// </summary>
/// <param name="Id">The preset's id, for example <c>half</c>, JSON <c>id</c>; a request's amount names it.</param>
/// <param name="Name">The display name, JSON <c>name</c>.</param>
/// <param name="Percent">The share of the base, 1 to 100, JSON <c>percent</c>.</param>
public sealed record ContentAmountPreset(string Id, string Name, int Percent);
