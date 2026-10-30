#nullable enable

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The real <see cref="IApiTransport"/>: one <see cref="UnityWebRequest"/> per call, awaited as a <see cref="Task"/> so the caller
    /// continues on Unity's main thread. It sends and receives text and decides nothing about what the text means.
    /// </summary>
    public sealed class UnityWebRequestTransport : IApiTransport
    {
        /// <inheritdoc/>
        public async Task<ApiTransportResponse> SendAsync(ApiTransportRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (UnityWebRequest web = new UnityWebRequest(request.Url, request.Method))
            {
                web.downloadHandler = new DownloadHandlerBuffer();
                web.SetRequestHeader("Accept", "application/json");
                if (request.JsonBody != null)
                {
                    web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.JsonBody));
                    web.SetRequestHeader("Content-Type", "application/json");
                }

                if (request.AccessToken != null)
                {
                    web.SetRequestHeader("Authorization", "Bearer " + request.AccessToken);
                }

                await SendAndWaitAsync(web, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return ReadResponse(web);
            }
        }

        /// <summary>Starts <paramref name="web"/> and completes when Unity reports it done; cancelling the token aborts the request.</summary>
        private static async Task SendAndWaitAsync(UnityWebRequest web, CancellationToken cancellationToken)
        {
            // RunContinuationsAsynchronously keeps Unity's completion callback from running our continuation inside its own call.
            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            AsyncOperation operation = web.SendWebRequest();
            operation.completed += _ => done.TrySetResult(true);

            // Abort ends the operation, so the wait below returns and the caller turns the cancelled token into the exception.
            using (cancellationToken.Register(web.Abort))
            {
                await done.Task;
            }
        }

        /// <summary>Copies a finished request into a response: an HTTP answer of any status, or a network failure.</summary>
        private static ApiTransportResponse ReadResponse(UnityWebRequest web)
        {
            bool hasHttpAnswer = web.result == UnityWebRequest.Result.Success || web.result == UnityWebRequest.Result.ProtocolError;
            if (!hasHttpAnswer)
            {
                return ApiTransportResponse.NetworkFailure(web.error ?? web.result.ToString());
            }

            return ApiTransportResponse.Http((int)web.responseCode, web.downloadHandler.text ?? string.Empty, web.GetResponseHeader("Retry-After"));
        }
    }
}
