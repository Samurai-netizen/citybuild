using System.Diagnostics.CodeAnalysis;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Ledger;
using CityBuilder.Domain.Players;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Players;

/// <summary>
/// The development login (<c>core-instructions/05-api-contract.md</c>, <c>POST /api/v1/auth/dev-login</c>; M0 only, Development only): a
/// display name is the whole identity. The first login of a name creates the player and their starter city
/// (<see cref="CityFactory.CreateStarterCity"/>) in one transaction, with one <c>STARTING_GRANT</c> ledger entry per item of the starting
/// stock, so the ledger reconciles with the stock from the first row. A later login of the name finds the same player. Scoped.
/// </summary>
/// <param name="players">The stored players.</param>
/// <param name="newCities">Stores the new city: the one city write without a row lock (ADR-0009).</param>
/// <param name="unitOfWork">The transaction and the writes.</param>
/// <param name="content">The game content; the starter city comes from it.</param>
/// <param name="timeProvider">The server clock: the ids' time, and the second the new city is settled at.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class DevLogin(
    IPlayerRepository players, INewCityRepository newCities, IUnitOfWork unitOfWork, IContentProvider content, TimeProvider timeProvider)
{
    /// <summary>The longest display name: 32 characters.</summary>
    public const int MaxDisplayNameLength = 32;

    /// <summary>Finds the players by name and adds the new one.</summary>
    private readonly IPlayerRepository _players = players ?? throw new ArgumentNullException(nameof(players));
    /// <summary>Adds the new player's city.</summary>
    private readonly INewCityRepository _newCities = newCities ?? throw new ArgumentNullException(nameof(newCities));
    /// <summary>Begins, writes and commits the first login's transaction.</summary>
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    /// <summary>The game content.</summary>
    private readonly IContentProvider _content = content ?? throw new ArgumentNullException(nameof(content));
    /// <summary>The server clock.</summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Whether a display name is allowed: 1 to <see cref="MaxDisplayNameLength"/> characters, each an ASCII letter, a digit, <c>_</c>,
    /// <c>-</c> or <c>.</c>. Names are compared exactly, so <c>dev1</c> and <c>Dev1</c> are two players.
    /// </summary>
    /// <param name="displayName">The name, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the name is allowed.</returns>
    public static bool IsValidDisplayName([NotNullWhen(true)] string? displayName) =>
        displayName is { Length: > 0 and <= MaxDisplayNameLength }
        && displayName.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

    /// <summary>
    /// Logs the name in: returns its player, and on the first login creates the player and their starter city. Two first logins of one
    /// name that race end with one player: the one whose insert commits second reads the other's player.
    /// </summary>
    /// <param name="displayName">The display name; see <see cref="IsValidDisplayName"/>.</param>
    /// <param name="cancellationToken">Cancels the login; nothing is committed then.</param>
    /// <returns>The player and whether this login created them.</returns>
    /// <exception cref="ArgumentException">The display name isn't allowed.</exception>
    public async Task<DevLoginResult> ExecuteAsync(string displayName, CancellationToken cancellationToken)
    {
        if (!IsValidDisplayName(displayName))
        {
            throw new ArgumentException(
                $"A display name is 1 to {MaxDisplayNameLength} ASCII letters, digits, '_', '-' or '.'.", nameof(displayName));
        }

        Player? existing = await _players.FindByDevNameAsync(displayName, cancellationToken);
        if (existing is not null)
        {
            return new DevLoginResult(existing.Id, IsNewPlayer: false);
        }

        try
        {
            return new DevLoginResult(await CreateAsync(displayName, cancellationToken), IsNewPlayer: true);
        }
        catch (DuplicateKeyException)
        {
            // Another first login of the name committed between the lookup and the insert (unique(dev_name)); this transaction is rolled
            // back. Its player is the one, unless the key was something else.
            Player winner = await _players.FindByDevNameAsync(displayName, cancellationToken)
                ?? throw new InvalidOperationException($"The first login of '{displayName}' hit a duplicate key, but no player has the name.");
            return new DevLoginResult(winner.Id, IsNewPlayer: false);
        }
    }

    /// <summary>
    /// Creates the player and their starter city in one transaction: the player, the city (clock 0, settled at the server's current second,
    /// with the content's version) and a <c>STARTING_GRANT</c> entry per item of the starting stock, in item order.
    /// </summary>
    /// <param name="displayName">The new player's name.</param>
    /// <param name="cancellationToken">Cancels the creation; nothing is committed then.</param>
    /// <returns>The new player's id.</returns>
    /// <exception cref="DuplicateKeyException">Another request stored a player with the name first.</exception>
    private async Task<Guid> CreateAsync(string displayName, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        GameContent content = _content.Content;
        var player = new Player(Guid.CreateVersion7(now), displayName);
        CityState state = CityFactory.CreateStarterCity(content);
        var city = new CityAggregate(
            Guid.CreateVersion7(now), player.Id, state, [], now.ToUnixTimeSeconds(), stateVersion: 0, content.ContentVersion);
        LedgerEntry[] startingGrant = state.Inventory
            .Where(stock => stock.Value > 0)
            .OrderBy(stock => stock.Key)
            .Select(stock => new LedgerEntry(stock.Key, stock.Value, stock.Value, LedgerReason.StartingGrant))
            .ToArray();

        await using IUnitOfWorkTransaction transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        _players.Add(player);
        _newCities.Add(city, startingGrant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return player.Id;
    }
}
