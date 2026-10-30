#nullable enable

using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Loads the game content (<c>GET /content</c>) and builds the engine's <see cref="GameContent"/> from it through the same validating
    /// factory the server uses (<see cref="GameContentFactory"/>), so the client never trusts content it could not have loaded itself. The
    /// client has no game numbers of its own (<c>03</c>): everything it shows and predicts comes from this object.
    /// </summary>
    public sealed class ContentService
    {
        /// <summary>The client that sends the request.</summary>
        private readonly ApiClient _api;

        /// <summary>Creates the service.</summary>
        /// <param name="api">The client; it must carry a token (the content endpoint needs one).</param>
        public ContentService(ApiClient api)
        {
            _api = api;
        }

        /// <summary>Fetches and validates the content.</summary>
        /// <param name="cancellationToken">Cancels the request, which then throws.</param>
        /// <returns>The content, the failed request, or the factory's errors.</returns>
        /// <exception cref="System.OperationCanceledException">The token was cancelled.</exception>
        public async Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            ApiResult<ContentResponseDto> response = await _api.GetContentAsync(cancellationToken);
            if (!response.IsSuccess)
            {
                return ContentLoadResult.RequestFailed(response.Error!);
            }

            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(response.Value!));
            return built.IsValid ? ContentLoadResult.Success(built.Content!) : ContentLoadResult.Invalid(built.Errors);
        }
    }
}
