#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using UnityEngine;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Keeps the client's picture of the city current and says how the connection stands (M0-41, <c>09</c> Runtime structure). It polls
    /// <c>GET /city</c> into the <see cref="ClientCityStore"/> whenever the <see cref="PollScheduler"/> says a poll is due (every 120 s in the
    /// foreground, on resuming from the background, after the network came back, and when asked) and turns each answer into a
    /// <see cref="ConnectionState"/> the views show. It sends no command and decides nothing about the city: the store keeps the newest
    /// snapshot, whichever of a poll or a command brought it.
    /// <para>
    /// The rules of the state machine:
    /// a usable snapshot of the content the client holds is <see cref="ConnectionState.Connected"/>; no answer is <see cref="ConnectionState.Offline"/>;
    /// a 5xx or an unexpected refusal is <see cref="ConnectionState.ServerError"/>; an answer that is not the contract's, or a snapshot of other
    /// content, is <see cref="ConnectionState.IncompatibleVersion"/> and is not applied; <c>AUTHENTICATION_REQUIRED</c> forgets the token, logs in
    /// once and repeats the request once, and only a second refusal (or a refused login) is <see cref="ConnectionState.Unauthorized"/>.
    /// <c>RATE_LIMITED</c> changes nothing: the next poll tries again. Every state is left by the next usable answer.
    /// </para>
    /// </summary>
    public sealed class ConnectionMonitor
    {
        /// <summary>Sends <c>GET /city</c>.</summary>
        private readonly ApiClient _api;

        /// <summary>Logs in again when the token is refused.</summary>
        private readonly AuthService _auth;

        /// <summary>Receives every usable snapshot.</summary>
        private readonly ClientCityStore _store;

        /// <summary>The <c>contentVersion</c> of the content the client loaded at the start; a snapshot of another version cannot be used with it.</summary>
        private readonly string _contentVersion;

        /// <summary>The dev player to log in as.</summary>
        private readonly string _displayName;

        /// <summary>Says when a poll is due.</summary>
        private readonly PollScheduler _scheduler;

        /// <summary>Whether the device reports a network; a change from no to yes asks for a poll.</summary>
        private readonly Func<bool> _isReachable;

        /// <summary>What <see cref="_isReachable"/> said at the last tick.</summary>
        private bool _wasReachable;

        /// <summary>
        /// Creates the monitor for a client that has just connected: the state is <see cref="ConnectionState.Connected"/> and the first poll is
        /// due one interval after the scheduler was made.
        /// </summary>
        /// <param name="api">The client; the same one the commands use.</param>
        /// <param name="auth">The auth service over <paramref name="api"/>.</param>
        /// <param name="store">The city store the snapshots go to.</param>
        /// <param name="contentVersion">The version of the content the client holds (<c>GameContent.ContentVersion</c>).</param>
        /// <param name="displayName">The dev player's name for a new login.</param>
        /// <param name="scheduler">Decides when to poll.</param>
        /// <param name="isReachable">Whether the device has a network (Unity: <c>Application.internetReachability</c>).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ConnectionMonitor(
            ApiClient api,
            AuthService auth,
            ClientCityStore store,
            string contentVersion,
            string displayName,
            PollScheduler scheduler,
            Func<bool> isReachable)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _contentVersion = contentVersion ?? throw new ArgumentNullException(nameof(contentVersion));
            _displayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _isReachable = isReachable ?? throw new ArgumentNullException(nameof(isReachable));
            _wasReachable = _isReachable();
            State = ConnectionState.Connected;
        }

        /// <summary>Raised after <see cref="State"/> or the code of <see cref="LastError"/> changed.</summary>
        public event Action? StateChanged;

        /// <summary>Raised when a poll that <see cref="TickAsync"/> began has ended, whatever its result (M0-55): the banner's "Trying…" ends with it.</summary>
        public event Action? PollFinished;

        /// <summary>How the connection stands now.</summary>
        public ConnectionState State { get; private set; }

        /// <summary>Why the state is not <see cref="ConnectionState.Connected"/>; null when connected, or when a snapshot of other content is the reason.</summary>
        public ApiError? LastError { get; private set; }

        /// <summary>
        /// The state a failed request means, or null when it says nothing about the connection (a rate limit on a poll). A request that got
        /// no answer is offline; a 5xx is a server fault; an answer that is not the contract's is a fault when the status says so and
        /// otherwise a client that does not fit the server; a refused token is unauthorized; any other refusal of a poll is unexpected, and
        /// of a login it means the login is not accepted.
        /// </summary>
        /// <param name="error">The failure.</param>
        /// <param name="duringLogin">Whether it came from the new login rather than from <c>GET /city</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
        public static ConnectionState? StateFor(ApiError error, bool duringLogin)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (error.Status == 0)
            {
                return ConnectionState.Offline;
            }

            if (error.Code == ApiErrorCodes.RateLimited)
            {
                return duringLogin ? ConnectionState.Unauthorized : (ConnectionState?)null;
            }

            if (error.Status >= 500)
            {
                return ConnectionState.ServerError;
            }

            if (error.Code == ApiErrorCodes.AuthenticationRequired)
            {
                return ConnectionState.Unauthorized;
            }

            if (error.Code == ApiErrorCodes.ClientInvalidResponse)
            {
                return error.Status >= 200 && error.Status < 300 ? ConnectionState.IncompatibleVersion : ConnectionState.ServerError;
            }

            return duringLogin ? ConnectionState.Unauthorized : ConnectionState.ServerError;
        }

        /// <summary>The app went to the background (<paramref name="paused"/>) or came back: no poll in the background, one at once on return.</summary>
        public void SetPaused(bool paused)
        {
            if (paused)
            {
                _scheduler.Pause();
            }
            else
            {
                _scheduler.Resume();
            }
        }

        /// <summary>Asks for a poll as soon as the app is in the foreground and none is running (for example after a command was refused with <c>AUTHENTICATION_REQUIRED</c>).</summary>
        public void RequestPoll()
        {
            _scheduler.RequestPoll();
        }

        /// <summary>
        /// A command ended with <paramref name="error"/>. A refused token means the access token ran out: a poll follows at once, and its one new
        /// login means the player's next try is accepted (M0-54). A command that got no answer or a server fault (the sender gave up after its
        /// repeats) says the connection is bad, so a poll follows at once too and the state turns Offline or ServerError without waiting for the
        /// next scheduled poll (M0-55). Every other error is the command's own and changes nothing here.
        /// </summary>
        /// <param name="error">The command's error.</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
        public void ReportCommandError(ApiError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (error.Code == ApiErrorCodes.AuthenticationRequired || error.Status == 0 || error.Status >= 500)
            {
                _scheduler.RequestPoll();
            }
        }

        /// <summary>
        /// Call it every frame (or from any timer): when the network just came back it asks for a poll, and when a poll is due it makes it.
        /// Returns when the poll is done, or at once when nothing is due. Never throws: a cancel ends it quietly.
        /// </summary>
        /// <param name="cancellationToken">Cancels the request (the app is closing).</param>
        public Task TickAsync(CancellationToken cancellationToken)
        {
            bool reachable = _isReachable();
            if (reachable && !_wasReachable)
            {
                _scheduler.RequestPoll();
            }

            _wasReachable = reachable;

            // Not async on purpose: a frame with nothing due must not allocate a task.
            return _scheduler.TryBegin() ? PollBegunAsync(cancellationToken) : Task.CompletedTask;
        }

        /// <summary>Polls after <see cref="PollScheduler.TryBegin"/> said so and always ends the scheduler's poll; a cancel ends it quietly.</summary>
        private async Task PollBegunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await PollAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The app is closing: the state stays as it was.
            }
            catch (Exception exception)
            {
                // The frame loop discards this task, so without the log nothing would ever show this. The next poll tries again.
                Debug.LogException(exception);
            }
            finally
            {
                _scheduler.End();
                PollFinished?.Invoke();
            }
        }

        /// <summary>
        /// Fetches the city now, whatever the scheduler says, and moves <see cref="State"/> by the answer (see the type's rules). A usable
        /// snapshot goes to the store. A refused token gets one new login and one repeat of the request.
        /// </summary>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
        public async Task PollAsync(CancellationToken cancellationToken)
        {
            ApiResult<CitySnapshotDto> answer = await _api.GetCityAsync(cancellationToken);
            bool loggedInAgain = false;
            if (!answer.IsSuccess && answer.Error!.Code == ApiErrorCodes.AuthenticationRequired)
            {
                loggedInAgain = true;
                _auth.Forget();
                AuthState login = await _auth.LoginAsync(_displayName, cancellationToken);
                if (login != AuthState.LoggedIn)
                {
                    Move(StateFor(_auth.LastError!, true), _auth.LastError);
                    return;
                }

                answer = await _api.GetCityAsync(cancellationToken);
            }

            if (answer.IsSuccess && answer.Value!.ContentVersion != _contentVersion)
            {
                // Content of another version: the client's rules and names do not fit it, so the snapshot stays out of the store.
                Move(ConnectionState.IncompatibleVersion, null);
                return;
            }

            // After a new login the answer may be another city (a reset development database), whose lower version the store would refuse as old.
            ApiError? failure = loggedInAgain ? SnapshotMapper.Replace(_store, answer) : SnapshotMapper.Apply(_store, answer);
            if (failure == null)
            {
                Move(ConnectionState.Connected, null);
                return;
            }

            Move(StateFor(failure, false), failure);
        }

        /// <summary>Sets the state and the error, and raises <see cref="StateChanged"/> when either the state or the error code differs; null for <paramref name="state"/> changes nothing.</summary>
        private void Move(ConnectionState? state, ApiError? error)
        {
            if (state == null)
            {
                return;
            }

            bool changed = state.Value != State || (error == null ? LastError != null : LastError == null || LastError.Code != error.Code);
            State = state.Value;
            LastError = error;
            if (changed)
            {
                StateChanged?.Invoke();
            }
        }
    }
}
