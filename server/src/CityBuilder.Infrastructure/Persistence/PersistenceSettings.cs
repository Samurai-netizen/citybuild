namespace CityBuilder.Infrastructure.Persistence;

/// <summary>
/// What the persistence needs from the configuration (<see cref="PersistenceServiceCollectionExtensions.AddCityBuilderPersistence"/>); the
/// Api builds it from its validated options (docs/getting-started.md → Configuration).
/// </summary>
/// <param name="ConnectionString">The PostgreSQL connection string.</param>
/// <param name="LockTimeoutMilliseconds">How long a city transaction waits for a row lock before it fails as a concurrency conflict
/// (<c>lock_timeout</c>, core-instructions/06-data-model.md: about 3 s); 1 or more.</param>
public sealed record PersistenceSettings(string ConnectionString, int LockTimeoutMilliseconds);
