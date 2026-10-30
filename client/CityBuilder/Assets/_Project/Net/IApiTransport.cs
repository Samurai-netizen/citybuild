#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The wire under <see cref="ApiClient"/>: it sends one request and returns what came back. There are two implementations,
    /// <see cref="UnityWebRequestTransport"/> for the game and a fake in the tests; the seam exists so timeouts, errors and cancellation
    /// can be tested without a network (<c>14</c>: an interface that makes testing possible).
    /// </summary>
    public interface IApiTransport
    {
        /// <summary>
        /// Sends <paramref name="request"/>. A failure to reach the server is a returned <see cref="ApiTransportResponse.NetworkFailure"/>,
        /// not an exception.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the request, whatever the reason (the caller, a timeout); it must then throw <see cref="System.OperationCanceledException"/>.</param>
        /// <exception cref="System.OperationCanceledException">The token was cancelled.</exception>
        Task<ApiTransportResponse> SendAsync(ApiTransportRequest request, CancellationToken cancellationToken);
    }
}
