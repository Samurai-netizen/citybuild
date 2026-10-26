namespace CityBuilder.Domain.Players;

/// <summary>
/// A player of the M0 development login (<c>core-instructions/06-data-model.md</c>, <c>players</c>): an id and the name the dev login
/// uses. Real accounts come in M1. Immutable.
/// </summary>
public sealed class Player
{
    /// <summary>Creates the player, for example when the dev login sees a new name or a repository loads one.</summary>
    /// <param name="id">The player's id, set by the application; not <see cref="Guid.Empty"/>.</param>
    /// <param name="devName">The name the dev login uses, unique per player; not blank.</param>
    /// <exception cref="ArgumentException">The id is empty or the name is blank.</exception>
    public Player(Guid id, string devName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A player id must not be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(devName);

        Id = id;
        DevName = devName;
    }

    /// <summary>The player's id; the city and the command receipts belong to it.</summary>
    public Guid Id { get; }

    /// <summary>The name the dev login uses, unique per player.</summary>
    public string DevName { get; }
}
