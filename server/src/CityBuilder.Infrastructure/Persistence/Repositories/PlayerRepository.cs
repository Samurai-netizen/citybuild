using CityBuilder.Application.Players;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary><see cref="IPlayerRepository"/> over <see cref="CityBuilderDbContext"/>, mapping <see cref="PlayerRow"/> explicitly. Scoped, like the context.</summary>
/// <param name="db">The request's context.</param>
/// <param name="timeProvider">The server clock; it stamps <c>created_at</c> of a new player.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class PlayerRepository(CityBuilderDbContext db, TimeProvider timeProvider) : IPlayerRepository
{
    /// <summary>The request's context.</summary>
    private readonly CityBuilderDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    /// <summary>The server clock for <c>created_at</c>.</summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async Task<Player?> FindByDevNameAsync(string devName, CancellationToken cancellationToken)
    {
        // A read that changes nothing: not tracked (06, EF Core conventions).
        PlayerRow? row = await _db.Players.AsNoTracking().SingleOrDefaultAsync(player => player.DevName == devName, cancellationToken);
        return row is null ? null : new Player(row.Id, row.DevName);
    }

    /// <inheritdoc />
    public void Add(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        _db.Players.Add(new PlayerRow { Id = player.Id, DevName = player.DevName, CreatedAt = _timeProvider.GetUtcNow() });
    }
}
