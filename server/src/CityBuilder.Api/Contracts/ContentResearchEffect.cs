namespace CityBuilder.Api.Contracts;

/// <summary>
/// One effect of a <see cref="ContentResearch"/> node. One flat shape for every type, like the content file: <c>type</c> says which of the
/// other fields are set, and the others are <see langword="null"/>. The types are <c>unlockBuilding</c> (<c>building</c>), <c>laborReduction</c>
/// (<c>building</c>, <c>permille</c>), <c>storageCapBonus</c> (<c>item</c>, <c>amount</c>), <c>unlockAmountMode</c> (<c>mode</c>) and
/// <c>walkSpeedBonus</c> (<c>permille</c>) (the last two CR-0008); a new type is an engine change (<c>core-instructions/25-eras-and-research.md</c>).
/// </summary>
/// <param name="Type">The effect type, JSON <c>type</c>.</param>
/// <param name="Building">The building type the effect is about, JSON <c>building</c>; set for <c>unlockBuilding</c> and <c>laborReduction</c>.</param>
/// <param name="Item">The item whose cap grows, JSON <c>item</c>; set for <c>storageCapBonus</c>.</param>
/// <param name="Permille">How much of the building's cycle labor, or of every walk's time, is saved, in permille, JSON <c>permille</c>; set for
/// <c>laborReduction</c> and <c>walkSpeedBonus</c>.</param>
/// <param name="Amount">How much the good's store limit grows in every store that holds it, JSON <c>amount</c>; set for <c>storageCapBonus</c>.</param>
/// <param name="Mode">The amount mode it unlocks, JSON <c>mode</c>: <c>percent</c> or <c>exact</c>; set for <c>unlockAmountMode</c>.</param>
public sealed record ContentResearchEffect(string Type, string? Building, string? Item, int? Permille, long? Amount, string? Mode);
