namespace CityBuilder.Application.Commands.Roads;

/// <summary>
/// A tile a road command names (<c>core-instructions/05-api-contract.md</c>, <c>tiles: [{ x, y }]</c>): only where the player chose, never
/// what lies there or what it costs. A record with properties, so the command's canonical payload (<see cref="CommandPayload"/>) holds both
/// numbers.
/// </summary>
/// <param name="X">The tile column.</param>
/// <param name="Y">The tile row.</param>
public sealed record CommandTile(int X, int Y);
