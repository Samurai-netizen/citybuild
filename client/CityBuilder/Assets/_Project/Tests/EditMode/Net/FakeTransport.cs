#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// A stand-in for the network: it records every request and answers with what the test queued, or never answers (to test timeouts
    /// and cancellation). It is the only fake of <see cref="IApiTransport"/> the client tests use.
    /// </summary>
    public sealed class FakeTransport : IApiTransport
    {
        /// <summary>What the next calls return, oldest first; null stands for a call whose token is cancelled, the way the client's timeout does it.</summary>
        private readonly Queue<ApiTransportResponse?> _answers = new Queue<ApiTransportResponse?>();

        /// <summary>Whether a call waits until its token is cancelled instead of answering.</summary>
        private bool _neverAnswers;

        /// <summary>Every request received, oldest first.</summary>
        public List<ApiTransportRequest> Requests { get; } = new List<ApiTransportRequest>();

        /// <summary>Queues an HTTP answer for the next call.</summary>
        public FakeTransport Answer(int statusCode, string body, string? retryAfter = null)
        {
            _answers.Enqueue(ApiTransportResponse.Http(statusCode, body, retryAfter));
            return this;
        }

        /// <summary>Queues a network failure for the next call.</summary>
        public FakeTransport Fail(string message)
        {
            _answers.Enqueue(ApiTransportResponse.NetworkFailure(message));
            return this;
        }

        /// <summary>Queues a timeout for the next call: it ends with the cancellation that <see cref="ApiClient"/> turns into <c>CLIENT_TIMEOUT</c>, at once instead of after the real timeout.</summary>
        public FakeTransport TimeOut()
        {
            _answers.Enqueue(null);
            return this;
        }

        /// <summary>Makes every call wait for its token, as a server that never answers does.</summary>
        public FakeTransport NeverAnswer()
        {
            _neverAnswers = true;
            return this;
        }

        /// <inheritdoc/>
        public async Task<ApiTransportResponse> SendAsync(ApiTransportRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_neverAnswers)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (_answers.Count == 0)
            {
                throw new InvalidOperationException("The test queued no answer for " + request.Method + " " + request.Url);
            }

            ApiTransportResponse? answer = _answers.Dequeue();
            if (answer == null)
            {
                throw new OperationCanceledException();
            }

            return answer;
        }
    }
}
