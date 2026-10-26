namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>ledger_entries</c> (core-instructions/06-data-model.md): one change of one item in a city, so every resource change is
/// auditable (core-instructions/02-principles.md, rule 11).
/// </summary>
public sealed class LedgerEntryRow
{
    /// <summary><c>id</c>, the primary key, an identity column the database numbers.</summary>
    public long Id { get; set; }

    /// <summary><c>city_id</c>: the city (<see cref="CityRow"/>).</summary>
    public Guid CityId { get; set; }

    /// <summary><c>item_id</c>: the item's content id.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary><c>delta</c>: the change, positive or negative.</summary>
    public long Delta { get; set; }

    /// <summary><c>balance_after</c>: the item's quantity after the change.</summary>
    public long BalanceAfter { get; set; }

    /// <summary><c>reason</c>: why it changed, one of the ledger reasons in 06 (e.g. <c>STARTING_GRANT</c>, <c>COST_PLACE</c>).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary><c>ref_type</c>: the kind of thing that caused it (e.g. a command), or <see langword="null"/>.</summary>
    public string? RefType { get; set; }

    /// <summary><c>ref_id</c>: the id of the thing that caused it, or <see langword="null"/>.</summary>
    public string? RefId { get; set; }

    /// <summary><c>created_at</c>: when the change was recorded (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
