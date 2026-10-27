namespace CityBuilder.Api.Contracts;

/// <summary>
/// One effect of a <see cref="ContentResearch"/> node. One flat shape for every type, like the content file: <c>type</c> says which of the
/// other fields are set, and the others are <see langword="null"/>. The types are <c>unlockBuilding</c> (<c>building</c>), <c>laborReduction</c>
/// (<c>building</c>, <c>permille</c>) and <c>storageCapBonus</c> (<c>item</c>, <c>amount</c>); a new type is an engine change
/// (<c>core-instructions/25-eras-and-research.md</c>).
/// </summary>
/// <param name="Type">The effect type, JSON <c>type</c>.</param>
/// <param name="Building">The building type the effect is about, JSON <c>building</c>; set for <c>unlockBuilding</c> and <c>laborReduction</c>.</param>
/// <param name="Item">The item whose cap grows, JSON <c>item</c>; set for <c>storageCapBonus</c>.</param>
/// <param name="Permille">How much of the building's cycle labor is saved, in permille, JSON <c>permille</c>; set for <c>laborReduction</c>.</param>
/// <param name="Amount">How much the item's storage cap grows, JSON <c>amount</c>; set for <c>storageCapBonus</c>.</param>
public sealed record ContentResearchEffect(string Type, string? Building, string? Item, int? Permille, long? Amount);
