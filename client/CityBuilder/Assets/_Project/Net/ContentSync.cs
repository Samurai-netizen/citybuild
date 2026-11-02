#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Keeps the city snapshots and the game content in step (M0-58, <c>05</c> Versioning). A snapshot names the <c>contentVersion</c> the server
    /// settled the city with. When it is the version the client holds, the snapshot is applied at once. When it is another, the client's rules
    /// and names do not fit it, so <c>GET /content</c> is fetched <b>first</b>, and only a content that loads, validates and is the snapshot's own
    /// version lets the snapshot in; the order is then <see cref="ContentReplacing"/> (the parts built on the old content let go),
    /// the snapshot goes to the store, <see cref="ContentReplaced"/> (the parts are built again on the new content, from that snapshot).
    /// Anything else (the content cannot be fetched, is invalid, or is not the snapshot's version) applies nothing and is
    /// <c>CLIENT_INVALID_RESPONSE</c> with a 2xx status, which <see cref="ConnectionMonitor.StateFor"/> turns into
    /// <see cref="CityBuilder.Client.Core.ConnectionState.IncompatibleVersion"/>. The poll and every command answer go through the one instance,
    /// so they cannot disagree about the content.
    /// </summary>
    public sealed class ContentSync
    {
        /// <summary>Fetches the content.</summary>
        private readonly ContentService _service;

        /// <summary>Lets one refetch run at a time: a poll and a command answer that both see new content do not fetch it twice.</summary>
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        /// <summary>Creates the sync for a client that holds content of <paramref name="loadedVersion"/>.</summary>
        /// <param name="service">The content service over the client's API client.</param>
        /// <param name="loadedVersion">The <c>contentVersion</c> of the content the client holds now.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ContentSync(ContentService service, string loadedVersion)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            LoadedVersion = loadedVersion ?? throw new ArgumentNullException(nameof(loadedVersion));
        }

        /// <summary>Raised when new content is ready and its snapshot is about to be applied: the parts built on the old content must stop reading the store.</summary>
        public event Action? ContentReplacing;

        /// <summary>Raised after the snapshot of the new content is in the store, with that content and the server's clock speed: build again on it.</summary>
        public event Action<GameContent, int>? ContentReplaced;

        /// <summary>The <c>contentVersion</c> of the content the client holds now.</summary>
        public string LoadedVersion { get; private set; }

        /// <summary>
        /// Applies a server answer, fetching new content first when its snapshot is of another version (see the type's rules). An answer that is a
        /// failure, or of the held version, is just given to <paramref name="apply"/>.
        /// </summary>
        /// <param name="answer">The answer of <c>GET /city</c> or of a command.</param>
        /// <param name="apply">Gives the answer to the store (<c>SnapshotMapper.Apply</c> or <c>Replace</c>) and returns its failure, or null.</param>
        /// <param name="cancellationToken">Cancels the fetch.</param>
        /// <returns>Null when the answer was applied; otherwise why it was not (the answer's own error, or the incompatibility).</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="OperationCanceledException">The token was cancelled during the fetch.</exception>
        public async Task<ApiError?> ApplyAsync(ApiResult<CitySnapshotDto> answer, Func<ApiError?> apply, CancellationToken cancellationToken)
        {
            if (answer == null)
            {
                throw new ArgumentNullException(nameof(answer));
            }

            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            if (!answer.IsSuccess || answer.Value!.ContentVersion == LoadedVersion)
            {
                return apply();
            }

            string wanted = answer.Value.ContentVersion;
            await _gate.WaitAsync(cancellationToken);
            try
            {
                // Another answer may have fetched this very content while this one waited for the gate.
                if (wanted == LoadedVersion)
                {
                    return apply();
                }

                ContentLoadResult loaded = await _service.LoadAsync(cancellationToken);
                string? problem = Problem(loaded, wanted);
                if (problem != null)
                {
                    return Incompatible(answer.Value, problem);
                }

                // Check the snapshot before anything is let go: the parts must not be torn down for a snapshot the store would then refuse.
                ApiResult<CitySnapshot> mapped = SnapshotMapper.ToSnapshot(answer.Value);
                if (!mapped.IsSuccess)
                {
                    return mapped.Error;
                }

                Raise(() => ContentReplacing?.Invoke());
                LoadedVersion = wanted;
                ApiError? failure = apply();
                Raise(() => ContentReplaced?.Invoke(loaded.Content!, loaded.TimeScale));
                return failure;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Runs a listener call; whatever a listener throws is logged, never passed on, as for <see cref="CommandSender.Completed"/>: the answer is
        /// already on its way into the store, and the poll or the command that brought it must not fail because a screen could not be built.
        /// </summary>
        private static void Raise(Action call)
        {
            try
            {
                call();
            }
            catch (Exception exception)
            {
                ClientLog.Exception(exception);
            }
        }

        /// <summary>Why the fetched content cannot be used for a snapshot of <paramref name="wanted"/>, or null when it can.</summary>
        private static string? Problem(ContentLoadResult loaded, string wanted)
        {
            if (!loaded.IsSuccess)
            {
                string why = loaded.ApiError != null ? loaded.ApiError.ToString() : loaded.ContentErrors.Count + " content error(s)";
                return "the content could not be loaded: " + why;
            }

            return loaded.Content!.ContentVersion == wanted
                ? null
                : "the server's content is version " + loaded.Content.ContentVersion + ", not " + wanted;
        }

        /// <summary>The error of a snapshot whose content the client cannot get: an answer that does not fit the client.</summary>
        private static ApiError Incompatible(CitySnapshotDto snapshot, string problem)
        {
            string detail = "The city is of content " + snapshot.ContentVersion + " and " + problem + ".";
            return new ApiError(ApiErrorCodes.ClientInvalidResponse, 200, detail, null, snapshot.RequestId, null);
        }
    }
}
