namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>A row of <c>players</c> (core-instructions/06-data-model.md): a player of the M0 dev login; real accounts come in M1.</summary>
public sealed class PlayerRow
{
    /// <summary><c>id</c>, the primary key; set by the application, not the database.</summary>
    public Guid Id { get; set; }

    /// <summary><c>dev_name</c>: the name the dev login uses, unique.</summary>
    public string DevName { get; set; } = string.Empty;

    /// <summary><c>created_at</c>: when the player was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
