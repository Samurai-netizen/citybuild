using CityBuilder.Application.Metrics;
using CityBuilder.Domain.Metrics;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="IPlayerEventRepository"/> over <see cref="CityBuilderDbContext"/>: stages one <c>player_events</c> row per event; the
/// request's unit of work writes them with the city's save, in its transaction. Scoped, one per request, like the context.
/// </summary>
/// <param name="db">The request's context.</param>
/// <exception cref="ArgumentNullException"><paramref name="db"/> is null.</exception>
public sealed class PlayerEventRepository(CityBuilderDbContext db) : IPlayerEventRepository
{
    /// <summary>The request's context.</summary>
    private readonly CityBuilderDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public void Add(IReadOnlyList<PlayerEvent> events) => throw new NotImplementedException();
}
