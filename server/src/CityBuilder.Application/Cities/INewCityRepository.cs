using CityBuilder.Domain.Ledger;

namespace CityBuilder.Application.Cities;

/// <summary>
/// The port that stores a new player's city (<c>docs/adr/ADR-0009-row-lock-own-city.md</c>): the one city write without the city's row lock,
/// because an insert has no row to lock yet; <c>unique(player_id)</c> makes a second city of the same player fail. Only
/// <see cref="Players.DevLogin"/> uses it (an architecture test checks it); every change of an existing city goes through
/// <see cref="ICityTransactionRunner"/>. <see cref="Persistence.IUnitOfWork.SaveChangesAsync"/> writes the insert.
/// </summary>
public interface INewCityRepository
{
    /// <summary>Stages the new city with all its rows and the ledger entries of its starting stock, stored at state version 1.</summary>
    /// <param name="city">The new city, at state version 0; its player is new as well, or already stored.</param>
    /// <param name="startingGrant">The <c>STARTING_GRANT</c> ledger entries of the city's starting stock, in item order.</param>
    /// <exception cref="InvalidOperationException">The city was loaded or staged by this request already, or its state version isn't 0.</exception>
    void Add(CityAggregate city, IReadOnlyList<LedgerEntry> startingGrant);
}
