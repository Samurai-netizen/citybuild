using CityBuilder.Application.Commands;
using CityBuilder.Domain.Commands;
using CityBuilder.Infrastructure.Persistence.Mapping;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="ICommandReceiptRepository"/> over <see cref="CityBuilderDbContext"/>: one read of <c>command_receipts</c> by its primary key,
/// not tracked (a receipt is never changed), inside the request's transaction when one is running. Receipts are written by
/// <see cref="CityRepository.Save"/>, with the city they describe. Scoped, one per request, like the context.
/// </summary>
/// <param name="db">The request's context.</param>
/// <exception cref="ArgumentNullException"><paramref name="db"/> is null.</exception>
public sealed class CommandReceiptRepository(CityBuilderDbContext db) : ICommandReceiptRepository
{
    /// <summary>The request's context.</summary>
    private readonly CityBuilderDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public async Task<CommandReceipt?> FindAsync(Guid playerId, Guid requestId, CancellationToken cancellationToken)
    {
        CommandReceiptRow? row = await _db.CommandReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(receipt => receipt.PlayerId == playerId && receipt.RequestId == requestId, cancellationToken);
        return row is null ? null : LedgerMapping.ToReceipt(row);
    }
}
