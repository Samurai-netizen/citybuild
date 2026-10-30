using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;

namespace CityBuilder.Application.Commands;

/// <summary>
/// The one way a command changes a city (<c>core-instructions/03-architecture.md</c>, request lifecycle; <c>05</c>, idempotency rules;
/// <c>06</c>, write transaction pattern; ADR-0004). In order: the shape check (before the database); the city's transaction with its row
/// lock (<see cref="ICityTransactionRunner"/>); the receipt of <c>(playerId, requestId)</c>: the same payload hash returns the stored response
/// and another hash <c>IDEMPOTENCY_CONFLICT</c>, both before the settle and writing nothing; the settle; the mutation (<see cref="ICityCommand.Apply"/>);
/// on a rule error the closing advance and the settlement only, without a receipt; otherwise the closing advance and one save of the city,
/// its ledger rows (settlement, then refunds and costs, then the closing advance) and the receipt with the response. A unique violation on the
/// receipt (an identical request committed first) rolls back, and the stored receipt answers. Scoped, one per request.
/// </summary>
/// <param name="transactions">Runs the command in the transaction that holds the city's row lock.</param>
/// <param name="receipts">Reads the stored receipts.</param>
/// <param name="content">The game content.</param>
/// <param name="timeProvider">The server clock, the only clock that drives progress.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class CommandPipeline(
    ICityTransactionRunner transactions, ICommandReceiptRepository receipts, IContentProvider content, TimeProvider timeProvider)
{
    /// <summary>Locks, loads and stores the acting player's city.</summary>
    private readonly ICityTransactionRunner _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
    /// <summary>Finds the receipt of a retried command.</summary>
    private readonly ICommandReceiptRepository _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
    /// <summary>The game content every command runs the engine with.</summary>
    private readonly IContentProvider _content = content ?? throw new ArgumentNullException(nameof(content));
    /// <summary>The server clock, read once per command, under the lock.</summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>Runs the player's command through every step, once.</summary>
    /// <param name="playerId">The acting player, from the access token.</param>
    /// <param name="command">The command.</param>
    /// <param name="writeResponse">Turns the applied command into the response JSON (the endpoint's snapshot); it runs inside the
    /// transaction, and its text is stored in the receipt as it is, so a replay returns the same text.</param>
    /// <param name="cancellationToken">Cancels the command; nothing is committed then.</param>
    /// <returns>How the command ended.</returns>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    /// <exception cref="ConcurrencyConflictException">The city's lock wasn't granted in time, or the city changed without it; nothing was
    /// committed and the command is safe to retry with the same request id.</exception>
    public async Task<CommandResult> ExecuteAsync(
        Guid playerId, ICityCommand command, Func<CommandSuccess, string> writeResponse, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(writeResponse);
        if (command.RequestId == Guid.Empty)
        {
            return CommandResult.InvalidArgument("requestId must be a UUID other than all zeros.");
        }

        if (command.ValidateShape() is { } shapeError)
        {
            return CommandResult.InvalidArgument(shapeError);
        }

        byte[] payloadHash = CommandPayload.Hash(command);
        try
        {
            return await _transactions.RunAsync(
                playerId, (transaction, token) => RunAsync(transaction, command, payloadHash, writeResponse, token), cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // An identical request committed its receipt first (06: unique violation on the receipt insert); this transaction rolled back
            // whole, its settlement included. The stored receipt answers, read outside the transaction. Any other key: not ours to handle.
            CommandReceipt? stored = await _receipts.FindAsync(playerId, command.RequestId, cancellationToken);
            if (stored is null)
            {
                throw;
            }

            return Answer(stored, payloadHash);
        }
    }

    /// <summary>The command's steps inside the city's transaction, after the row lock.</summary>
    /// <param name="transaction">The acting player's city, locked.</param>
    /// <param name="command">The command, its shape checked.</param>
    /// <param name="payloadHash">The hash of its canonical payload.</param>
    /// <param name="writeResponse">Turns the applied command into the response JSON.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>How the command ended; the runner commits what was saved.</returns>
    private async Task<CommandResult> RunAsync(
        CityTransaction transaction,
        ICityCommand command,
        byte[] payloadHash,
        Func<CommandSuccess, string> writeResponse,
        CancellationToken cancellationToken)
    {
        // Under the lock, so an identical request that waited for it sees the receipt the first one committed.
        CommandReceipt? stored = await _receipts.FindAsync(transaction.City.PlayerId, command.RequestId, cancellationToken);
        if (stored is not null)
        {
            // Before the settle: nothing is staged, so the runner's commit is empty, as a rollback would be.
            return Answer(stored, payloadHash);
        }

        CityAggregate city = transaction.City;
        GameContent content = _content.Content;
        DateTimeOffset now = _timeProvider.GetUtcNow();
        CitySettler settler = CitySettler.Begin(city, content, now);
        MutationResult mutation = command.Apply(city.State, content);
        if (mutation.ErrorCode is { } errorCode)
        {
            // Time passed is never undone (decision 014): the settlement is committed, but no receipt, so a new attempt needs a new id.
            Settlement rejectedAfter = settler.Close();
            if (settler.Changed)
            {
                await transaction.SaveAsync(settler.LedgerEntries(), receipt: null, cancellationToken);
            }

            return CommandResult.Rejected(errorCode, rejectedAfter);
        }

        settler.RecordCommand(mutation, command.CostReason, command.RefundReason, command.RequestId);
        Settlement settlement = settler.Close();
        IReadOnlyList<LedgerEntry> ledger = settler.LedgerEntries();
        string response = writeResponse(new CommandSuccess(command.RequestId, city, settlement, checked(city.StateVersion + 1), now));

        // A successful command always saves, also when it changed nothing: its receipt describes the saved state.
        await transaction.SaveAsync(ledger, new CommandReceipt(command.RequestId, command.CommandType, payloadHash, response), cancellationToken);
        return CommandResult.Succeeded(response, settlement);
    }

    /// <summary>The answer of a stored receipt: its response for the same payload, a conflict for another.</summary>
    /// <param name="stored">The receipt of the request id.</param>
    /// <param name="payloadHash">The hash of this request's payload.</param>
    /// <returns>A replay or an idempotency conflict.</returns>
    private static CommandResult Answer(CommandReceipt stored, byte[] payloadHash) =>
        stored.PayloadHash.SequenceEqual(payloadHash)
            ? CommandResult.Replayed(stored.ResponseJson)
            : CommandResult.IdempotencyConflict();
}
