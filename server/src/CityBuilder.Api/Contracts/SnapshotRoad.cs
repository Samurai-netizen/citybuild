namespace CityBuilder.Api.Contracts;

/// <summary>One road tile in the <c>roads</c> list of a <see cref="SnapshotCity"/> (<c>core-instructions/05-api-contract.md</c>, CR-0008).</summary>
/// <param name="X">The tile column, JSON <c>x</c>.</param>
/// <param name="Y">The tile row, JSON <c>y</c>.</param>
/// <param name="Level">The road level, JSON <c>level</c>: 1 when placed, up to the last road level of the content.</param>
public sealed record SnapshotRoad(int X, int Y, int Level);
