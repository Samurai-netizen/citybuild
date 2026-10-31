#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// A stand-in for the network whose answers the test gives when it chooses: every request waits until <see cref="Respond"/> (or
    /// <see cref="FailWithNetworkError"/>) answers the oldest open one, so a test can look at the screen while a command is on its way. It
    /// honours the request's cancellation token. (The EditMode <c>FakeTransport</c> answers at once; that is in another assembly.)
    /// </summary>
    public sealed class ControllableTransport : IApiTransport
    {
        /// <summary>The requests that have not been answered, oldest first.</summary>
        private readonly Queue<TaskCompletionSource<ApiTransportResponse>> _open = new Queue<TaskCompletionSource<ApiTransportResponse>>();

        /// <summary>Every request received, oldest first.</summary>
        public List<ApiTransportRequest> Requests { get; } = new List<ApiTransportRequest>();

        /// <summary>How many requests are waiting for an answer.</summary>
        public int OpenCount => _open.Count;

        /// <inheritdoc/>
        public async Task<ApiTransportResponse> SendAsync(ApiTransportRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var answer = new TaskCompletionSource<ApiTransportResponse>();
            _open.Enqueue(answer);
            using (cancellationToken.Register(() => answer.TrySetCanceled(cancellationToken)))
            {
                return await answer.Task;
            }
        }

        /// <summary>Answers the oldest open request with an HTTP answer.</summary>
        public void Respond(int statusCode, string body)
        {
            _open.Dequeue().SetResult(ApiTransportResponse.Http(statusCode, body, null));
        }

        /// <summary>Answers the oldest open request as if the connection broke.</summary>
        public void FailWithNetworkError()
        {
            _open.Dequeue().SetResult(ApiTransportResponse.NetworkFailure("test: connection lost"));
        }
    }
}
