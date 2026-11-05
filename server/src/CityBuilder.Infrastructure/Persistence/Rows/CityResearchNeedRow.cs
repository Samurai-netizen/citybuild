namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_research_needs</c> (core-instructions/06-data-model.md, CR-0008): how much of one good the running research still needs
/// at the science building. Rows exist only while it waits, and then its start and end are null.
/// </summary>
public sealed class CityResearchNeedRow
{
    /// <summary><c>city_id</c>: the city (<see cref="CityRow"/>); with <see cref="ItemId"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>item_id</c>: the good's content id.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary><c>quantity</c>: what is still missing, above 0; the row goes when it is delivered.</summary>
    public long Quantity { get; set; }
}
