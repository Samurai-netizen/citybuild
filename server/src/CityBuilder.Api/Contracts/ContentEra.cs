namespace CityBuilder.Api.Contracts;

/// <summary>One era in <see cref="ContentResponse.Eras"/> (<c>core-instructions/25-eras-and-research.md</c>).</summary>
/// <param name="Number">The era's number, from 1, JSON <c>number</c>.</param>
/// <param name="Id">The era's id, for example <c>early_medieval</c>, JSON <c>id</c>.</param>
/// <param name="Name">The display name, JSON <c>name</c>.</param>
public sealed record ContentEra(int Number, string Id, string Name);
