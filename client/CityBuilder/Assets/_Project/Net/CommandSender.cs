#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using UnityEngine;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The one way the client gives the server a command (<c>09</c> Runtime structure). It makes the request id once per player intent
    /// and sends it with every attempt, so the server's idempotency (<c>05</c>) makes a repeat harmless. It repeats a command only when the
    /// failure says nothing about the rules (<see cref="CommandPolicy.IsRetryable"/>), at most <see cref="CommandPolicy.MaxAttempts"/> times
    /// with a growing wait, and never repeats a refusal. A snapshot that comes back goes to the <see cref="ClientCityStore"/> through
    /// <see cref="SnapshotMapper"/>; nothing else changes the city, so there is no optimistic economy (<c>09</c> UX rules).
    /// <para>
    /// <see cref="Pending"/> lists the intents that have not ended, for the UI to show a ghost, a pending value or a disabled button.
    /// A repeat of a pending intent is the same intent and gets its result (a double tap sends once). Worker targets
    /// (<see cref="CommandKind.AssignWorkers"/>) are different: a new target for a building replaces a not-yet-sent one, and is sent after
    /// a short pause with no newer one, so rapid taps on the stepper become one command with the final value; a target is never sent while an
    /// earlier one of the same building is still on its way, so the server sees them in the order the player made them.
    /// </para>
    /// Everything runs on the main thread: call it from Unity events and <c>await</c> its tasks there.
    /// </summary>
    public sealed class CommandSender
    {
        /// <summary>Sends the requests.</summary>
        private readonly ApiClient _api;

        /// <summary>Takes the snapshot of every accepted command.</summary>
        private readonly ClientCityStore _store;

        /// <summary>How many times, how fast and how long to wait.</summary>
        private readonly CommandPolicy _policy;

        /// <summary>Waits for a time span or until the token is cancelled (then it throws <see cref="OperationCanceledException"/>); a test passes its own.</summary>
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;

        /// <summary>Makes the request id of a new intent.</summary>
        private readonly Func<Guid> _newRequestId;

        /// <summary>
        /// Raised once for every intent that ended with the server's answer or with its error, a success or a failure, after the pending list no
        /// longer holds it: the one place a screen can show a result from (M0-47). Not raised for a <see cref="CommandOutcomeKind.Superseded"/> target
        /// (nothing reached the server) or when the caller's token was cancelled (the game is closing). A listener that throws is logged and does
        /// not stop the sender.
        /// </summary>
        public event Action<CommandOutcome>? Completed;

        /// <summary>The intents that have not ended, oldest first.</summary>
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>The worker stepper of each building that has a target waiting or on its way, by building id.</summary>
        private readonly Dictionary<int, StepperLane> _lanes = new Dictionary<int, StepperLane>();

        /// <summary>The pending commands as handed out; replaced whenever an intent starts or ends, so a reader's list never changes under it.</summary>
        private PendingCommand[] _pending = Array.Empty<PendingCommand>();

        /// <summary>Creates a sender on the real clock with new random request ids.</summary>
        /// <param name="api">The API client.</param>
        /// <param name="store">The city store that takes the answers.</param>
        /// <param name="policy">The attempts, waits and debounce; <see cref="CommandPolicy.Default"/> in the game.</param>
        public CommandSender(ApiClient api, ClientCityStore store, CommandPolicy policy)
            : this(api, store, policy, Task.Delay, Guid.NewGuid)
        {
        }

        /// <summary>Creates a sender on the given clock and request id source.</summary>
        /// <param name="api">The API client.</param>
        /// <param name="store">The city store that takes the answers.</param>
        /// <param name="policy">The attempts, waits and debounce.</param>
        /// <param name="delay">Waits; a test passes one that finishes when the test says so.</param>
        /// <param name="newRequestId">Makes a request id; it must never return the all-zeros id (the server refuses it).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public CommandSender(
            ApiClient api,
            ClientCityStore store,
            CommandPolicy policy,
            Func<TimeSpan, CancellationToken, Task> delay,
            Func<Guid> newRequestId)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
            _newRequestId = newRequestId ?? throw new ArgumentNullException(nameof(newRequestId));
        }

        /// <summary>Raised when an intent starts or ends, and when a pending one changes <see cref="PendingCommand.Phase"/>.</summary>
        public event Action? PendingChanged;

        /// <summary>The intents that have not ended, oldest first. The list does not change; ask again after <see cref="PendingChanged"/>.</summary>
        public IReadOnlyList<PendingCommand> Pending => _pending;

        /// <summary>
        /// Gives the server <paramref name="intent"/> and waits for how it ended. A refusal, a lost connection or a replaced worker target
        /// are outcomes, not exceptions; only a cancelled token throws.
        /// </summary>
        /// <param name="intent">What the player wants.</param>
        /// <param name="cancellationToken">Cancels the wait and the sending (the object that asked was destroyed). Of two callers that share an intent only the first one's token counts.</param>
        /// <returns>
        /// How the intent ended. For a pending intent that is equal to this one, the result of that one.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="intent"/> is null.</exception>
        /// <exception cref="OperationCanceledException">The token was cancelled (also on the returned task).</exception>
        public Task<CommandOutcome> SendAsync(CommandIntent intent, CancellationToken cancellationToken)
        {
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (intent.Kind == CommandKind.AssignWorkers)
            {
                return SendWorkersAsync(intent, cancellationToken);
            }

            foreach (Entry pending in _entries)
            {
                if (pending.Command.Intent.Equals(intent))
                {
                    return pending.Completion.Task;
                }
            }

            Entry entry = Start(intent, null);
            _ = RunAsync(entry, null, cancellationToken);
            return entry.Completion.Task;
        }

        /// <summary>
        /// Starts a worker target: it replaces the building's waiting one, and is sent when the debounce has passed with no newer target and
        /// the building's earlier target is no longer on its way.
        /// </summary>
        private Task<CommandOutcome> SendWorkersAsync(CommandIntent intent, CancellationToken cancellationToken)
        {
            if (!_lanes.TryGetValue(intent.BuildingId, out StepperLane? lane))
            {
                lane = new StepperLane();
                _lanes.Add(intent.BuildingId, lane);
            }

            if (lane.Waiting != null)
            {
                Supersede(lane.Waiting);
            }

            Entry entry = Start(intent, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
            lane.Waiting = entry;
            _ = RunAsync(entry, lane, cancellationToken);
            return entry.Completion.Task;
        }

        /// <summary>
        /// Takes an intent through its whole life: the wait of a worker target, the attempts, the cleanup and the outcome, in that order, so
        /// whoever waits on the outcome finds the intent gone from <see cref="Pending"/>. It never throws: whatever happens ends in the entry's
        /// task, which is why it can run un-awaited.
        /// </summary>
        private async Task RunAsync(Entry entry, StepperLane? lane, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool>? turn = null;
            CommandOutcome? outcome = null;
            Exception? failure = null;
            try
            {
                bool isOurTurn = lane == null || await WaitForTurnAsync(entry, lane);
                if (isOurTurn)
                {
                    if (lane != null)
                    {
                        // Nothing of this building is on its way and no newer target has come.
                        lane.Waiting = null;
                        turn = new TaskCompletionSource<bool>();
                        lane.InFlight = turn.Task;
                    }

                    outcome = await SendWithRetriesAsync(entry, cancellationToken);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            // A replaced target has nothing to release: it is no longer the lane's, and the one that replaced it disposes its token source.
            if (!entry.IsSuperseded)
            {
                if (lane != null)
                {
                    ReleaseLane(entry, lane, turn != null);
                }

                // A source that was cancelled is left alone: this may be running inside its own cancel callback, where Dispose can block, and a
                // cancelled source holds nothing but memory. One that was not is disposed so it lets go of the caller's token.
                if (entry.Replaced != null && !entry.Replaced.IsCancellationRequested)
                {
                    entry.Replaced.Dispose();
                }
            }

            try
            {
                Remove(entry);
            }
            catch (Exception exception)
            {
                // A listener of the pending list threw. That is no reason to leave the caller's task unfinished and the lane blocked for good.
                Debug.LogException(exception);
            }

            if (outcome != null)
            {
                entry.Completion.TrySetResult(outcome);
                RaiseCompleted(outcome);
            }
            else if (failure is OperationCanceledException)
            {
                entry.Completion.TrySetCanceled(cancellationToken);
            }
            else if (failure != null)
            {
                entry.Completion.TrySetException(failure);
            }

            // A replaced target's task was completed by the one that replaced it.
            turn?.SetResult(true);
        }

        /// <summary>
        /// Lets go of the building's stepper when its entry ends: the target on its way is no longer on its way, a target waiting that was
        /// cancelled is no longer waiting, and an idle stepper is forgotten. A replaced target touches nothing: it is no longer the lane's.
        /// </summary>
        private void ReleaseLane(Entry entry, StepperLane lane, bool hadTurn)
        {
            if (hadTurn)
            {
                lane.InFlight = null;
            }
            else if (lane.Waiting == entry)
            {
                lane.Waiting = null;
            }

            int buildingId = entry.Command.Intent.BuildingId;
            if (lane.Waiting == null && lane.InFlight == null && _lanes.TryGetValue(buildingId, out StepperLane? registered) && registered == lane)
            {
                _lanes.Remove(buildingId);
            }
        }

        /// <summary>Waits out the debounce and then the building's earlier target.</summary>
        /// <returns>True when it is this target's turn to be sent; false when a newer target replaced it (its task is already complete).</returns>
        private async Task<bool> WaitForTurnAsync(Entry entry, StepperLane lane)
        {
            try
            {
                await _delay(_policy.StepperDebounce, entry.Replaced!.Token);
            }
            catch (OperationCanceledException) when (entry.IsSuperseded)
            {
                return false;
            }

            Task? onItsWay = lane.InFlight;
            while (!entry.IsSuperseded && onItsWay != null)
            {
                await onItsWay;
                onItsWay = lane.InFlight;
            }

            return !entry.IsSuperseded;
        }

        /// <summary>
        /// Sends the entry's command up to the policy's maximum, with the same request id each time, until it is accepted, refused for good or
        /// out of attempts.
        /// </summary>
        private async Task<CommandOutcome> SendWithRetriesAsync(Entry entry, CancellationToken cancellationToken)
        {
            PendingCommand command = entry.Command;
            for (int attempt = 1; ; attempt++)
            {
                command.Attempts = attempt;
                command.Phase = CommandPhase.Sending;
                PendingChanged?.Invoke();

                ApiResult<CitySnapshotDto> result = await command.Intent.SendAsync(_api, command.RequestId, cancellationToken);
                ApiError? error = SnapshotMapper.Apply(_store, result);
                if (error == null)
                {
                    return new CommandOutcome(CommandOutcomeKind.Succeeded, command, null);
                }

                if (attempt >= _policy.MaxAttempts || !CommandPolicy.IsRetryable(error))
                {
                    return new CommandOutcome(CommandOutcomeKind.Failed, command, error);
                }

                command.Phase = CommandPhase.Retrying;
                PendingChanged?.Invoke();
                await _delay(_policy.BackoffAfter(attempt), cancellationToken);
            }
        }

        /// <summary>Tells the listeners of <see cref="Completed"/> how an intent ended; whatever a listener throws is logged, never passed on.</summary>
        private void RaiseCompleted(CommandOutcome outcome)
        {
            try
            {
                Completed?.Invoke(outcome);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Ends a worker target that was never sent because a newer one came: its task completes now, and its runner finds out and stops. The
        /// runner may stop inside <c>Cancel</c> (a wait ends at once), so this is also where the token source is disposed, after it.
        /// </summary>
        private void Supersede(Entry entry)
        {
            entry.IsSuperseded = true;
            entry.Completion.TrySetResult(new CommandOutcome(CommandOutcomeKind.Superseded, entry.Command, null));
            Remove(entry);
            entry.Replaced!.Cancel();
            entry.Replaced.Dispose();
        }

        /// <summary>Makes the pending command of a new intent, with its request id, and lists it.</summary>
        private Entry Start(CommandIntent intent, CancellationTokenSource? replaced)
        {
            var entry = new Entry(new PendingCommand(_newRequestId(), intent), replaced);
            _entries.Add(entry);
            Publish();
            return entry;
        }

        /// <summary>Takes an entry off the list (it may already be off) and tells the listeners.</summary>
        private void Remove(Entry entry)
        {
            if (_entries.Remove(entry))
            {
                Publish();
            }
        }

        /// <summary>Rebuilds the list that <see cref="Pending"/> hands out and raises <see cref="PendingChanged"/>.</summary>
        private void Publish()
        {
            var list = new PendingCommand[_entries.Count];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = _entries[i].Command;
            }

            _pending = list;
            PendingChanged?.Invoke();
        }

        /// <summary>The sender's side of one pending command: the public part, the task the caller waits on and what the worker stepper needs.</summary>
        private sealed class Entry
        {
            /// <summary>Creates an entry.</summary>
            public Entry(PendingCommand command, CancellationTokenSource? replaced)
            {
                Command = command;
                Replaced = replaced;
            }

            /// <summary>What the UI sees.</summary>
            public PendingCommand Command { get; }

            /// <summary>Completes with how the intent ended; it is what <see cref="SendAsync"/> returns.</summary>
            public TaskCompletionSource<CommandOutcome> Completion { get; } = new TaskCompletionSource<CommandOutcome>();

            /// <summary>Cancelled when a newer worker target replaces this one, and when the caller cancels; null for the other commands.</summary>
            public CancellationTokenSource? Replaced { get; }

            /// <summary>Whether a newer worker target replaced this one before it was sent.</summary>
            public bool IsSuperseded { get; set; }
        }

        /// <summary>The worker targets of one building: the one waiting to be sent and the one on its way, at most one each.</summary>
        private sealed class StepperLane
        {
            /// <summary>The target in its debounce or waiting for the one on its way; null if none.</summary>
            public Entry? Waiting { get; set; }

            /// <summary>Completes when the target on its way has ended; null if none is on its way.</summary>
            public Task? InFlight { get; set; }
        }
    }
}
