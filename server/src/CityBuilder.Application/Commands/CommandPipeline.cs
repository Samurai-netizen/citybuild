using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Application.Metrics;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Domain.Metrics;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;

namespace CityBuilder.Application.Commands;

/// <summary>
/// The one way a command changes a city (<c>core-instructions/03-architecture.md</c>, request lifecycle; <c>05</c>, idempotency rules;
/// <c>06</c>, write transaction pattern; ADR-0004). In order: the shape check (before the database); the city's transaction with its row
/// lock (<see cref="ICityTransactionRunner"/>); the receipt of <c>(playerId, requestId)</c>: the same payload hash returns the stored response
/// and another hash <c>IDEMPOTENCY_CONFLICT</c>, both before the settle and writing nothing; the settle; the mutation (<see cref="ICityCommand.Apply"/>);
/// the settlement's completions and then the command's player event (CR-0009), on a rule error and on success, in the same transaction; on a rule error the closing advance and
/// the settlement only, without a receipt (and when nothing was settled, only the event); otherwise the closing advance and one save of the city,
/// its ledger rows (settlement, then refunds and costs, then the closing advance) and the receipt with the response. A unique violation on the
/// receipt (an identical request committed first) rolls back, and the stored receipt answers. Scoped, one per request.
/// </summary>
/// <param name="transactions">Runs the command in the transaction that holds the city's row lock.</param>
/// <param name="receipts">Reads the stored receipts.</param>
/// <param name="content">The game content.</param>
/// <param name="timeProvider">The server clock, the only clock that drives progress.</param>
/// <param name="presence">When the player counts as away, for the couriers' stamina (<c>04</c> section 2); a replay is no presence.</param>
/// <param name="sessions">When a command starts a play session (CR-0009, <c>session.start</c>); a replay starts none.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class CommandPipeline(
    ICityTransactionRunner transactions, ICommandReceiptRepository receipts, IContentProvider content, TimeProvider timeProvider,
    PresencePolicy presence, SessionPolicy sessions)
{
    /// <summary>Locks, loads and stores the acting player's city.</summary>
    private readonly ICityTransactionRunner _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
    /// <summary>Finds the receipt of a retried command.</summary>
    private readonly ICommandReceiptRepository _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
    /// <summary>The game content every command runs the engine with.</summary>
    private readonly IContentProvider _content = content ?? throw new ArgumentNullException(nameof(content));
    /// <summary>The server clock, read once per command, under the lock.</summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    /// <summary>When the player counts as away; the settle records the command as the player's last-seen second.</summary>
    private readonly PresencePolicy _presence = presence ?? throw new ArgumentNullException(nameof(presence));
    /// <summary>When the command starts a play session; read against the last-seen second before the settle marks the city seen.</summary>
    private readonly SessionPolicy _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));

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
        CitySettler settler = CitySettler.Begin(city, content, now, _presence);

        // The metrics' detail is read on the settled city before the mutation (an upgraded or placed building is named as it was asked for).
        string? detail = command.EventDetail(city.State, content);
        MutationResult mutation = command.Apply(city.State, content);
        if (mutation.ErrorCode is { } errorCode)
        {
            // Time passed is never undone (decision 014): the settlement is committed, but no receipt, so a new attempt needs a new id.
            Settlement rejectedAfter = settler.Close();
            RecordSettlement(transaction, rejectedAfter, now);
            transaction.Record(CommandEvent(city, command, detail, CommandEvents.Outcome(errorCode), now));
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
        RecordSettlement(transaction, settlement, now);
        transaction.Record(CommandEvent(city, command, detail, PlayerEventTypes.OutcomeAccepted, now));
        await transaction.SaveAsync(ledger, new CommandReceipt(command.RequestId, command.CommandType, payloadHash, response), cancellationToken);
        return CommandResult.Succeeded(response, settlement);
    }

    /// <summary>Records what the settlement completed (<see cref="SettlementEvents"/>), before the command's own event.</summary>
    /// <param name="transaction">The acting player's city, settled and closed.</param>
    /// <param name="settlement">The closed settlement.</param>
    /// <param name="now">The server time of the command.</param>
    private static void RecordSettlement(CityTransaction transaction, Settlement settlement, DateTimeOffset now)
    {
        foreach (PlayerEvent completed in SettlementEvents.Of(transaction.City, settlement, now))
        {
            transaction.Record(completed);
        }
    }

    /// <summary>
    /// The command's player event (CR-0009): <c>command.&lt;type&gt;</c> with its detail and outcome, at the settled city clock and the
    /// command's server second. Recorded on the transaction, so it is written exactly when the command's transaction commits.
    /// </summary>
    /// <param name="city">The acting player's city, settled and closed.</param>
    /// <param name="command">The command.</param>
    /// <param name="detail">The content id it is about, or <see langword="null"/>.</param>
    /// <param name="outcome"><c>accepted</c> or the error code.</param>
    /// <param name="now">The server time of the command.</param>
    /// <returns>The event.</returns>
    private static PlayerEvent CommandEvent(CityAggregate city, ICityCommand command, string? detail, string outcome, DateTimeOffset now) =>
        new(city.PlayerId, PlayerEventTypes.ForCommand(command.CommandType), detail, outcome, value: null, city.State.Clock, now);

    /// <summary>The answer of a stored receipt: its response for the same payload, a conflict for another.</summary>
    /// <param name="stored">The receipt of the request id.</param>
    /// <param name="payloadHash">The hash of this request's payload.</param>
    /// <returns>A replay or an idempotency conflict.</returns>
    private static CommandResult Answer(CommandReceipt stored, byte[] payloadHash) =>
        stored.PayloadHash.SequenceEqual(payloadHash)
            ? CommandResult.Replayed(stored.ResponseJson)
            : CommandResult.IdempotencyConflict();
}
