using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// <see cref="UnityWebRequestTransport"/> against a small HTTP server inside the test (<see cref="HttpListener"/> on a free local
    /// port): what it sends (method, path, headers, body) and what it returns for a 200, a Problem Details 422, a 429 with
    /// <c>Retry-After</c>, a refused connection and a cancelled call. The tests are coroutines because Unity completes web requests on its own update loop.
    /// </summary>
    public sealed class UnityWebRequestTransportTests
    {
        /// <summary>The server of the current test.</summary>
        private HttpListener _listener;

        /// <summary>The request the server last saw: method, path, headers and body.</summary>
        private volatile string _seen;

        /// <summary>The prefix the server listens on, for example <c>http://localhost:49213/</c>.</summary>
        private string _prefix;

        /// <summary>Starts a server that answers every request with <paramref name="status"/>, <paramref name="body"/> and an optional <c>Retry-After</c>.</summary>
        private void StartServer(int status, string body, string retryAfter = null)
        {
            _listener = new HttpListener();
            for (int port = 49200; port < 49400; port++)
            {
                try
                {
                    _prefix = "http://localhost:" + port + "/";
                    _listener.Prefixes.Clear();
                    _listener.Prefixes.Add(_prefix);
                    _listener.Start();
                    break;
                }
                catch (HttpListenerException)
                {
                    // The port is taken: try the next one.
                }
            }

            Task.Run(() => Serve(status, body, retryAfter));
        }

        /// <summary>Answers requests until the listener stops.</summary>
        private void Serve(int status, string body, string retryAfter)
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (Exception exception) when (exception is HttpListenerException || exception is ObjectDisposedException)
                {
                    return;
                }

                string requestBody = new StreamReader(context.Request.InputStream, Encoding.UTF8).ReadToEnd();
                _seen = context.Request.HttpMethod + " " + context.Request.Url.AbsolutePath + "\n"
                    + "auth=" + context.Request.Headers["Authorization"] + "\n"
                    + "type=" + context.Request.ContentType + "\n" + requestBody;
                context.Response.StatusCode = status;
                if (retryAfter != null)
                {
                    context.Response.AddHeader("Retry-After", retryAfter);
                }

                byte[] bytes = Encoding.UTF8.GetBytes(body);
                context.Response.ContentType = "application/json";
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.Close();
            }
        }

        /// <summary>Stops the server of the test.</summary>
        [TearDown]
        public void StopServer()
        {
            _listener?.Close();
            _listener = null;
        }

        /// <summary>Waits for <paramref name="task"/> to finish, one editor frame at a time, for at most 20 seconds.</summary>
        private static IEnumerator Wait(Task task)
        {
            DateTime until = DateTime.UtcNow.AddSeconds(20);
            while (!task.IsCompleted && DateTime.UtcNow < until)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "the request did not finish in 20 seconds");
        }

        /// <summary>A POST with a body and a token: the server sees the method, path, bearer header, JSON content type and body; the 200 body comes back.</summary>
        [UnityTest]
        public IEnumerator APost_SendsMethodPathHeadersAndBody_AndReturnsTheAnswer()
        {
            StartServer(200, "{\"ok\":true}");
            Task<ApiTransportResponse> call = new UnityWebRequestTransport().SendAsync(
                new ApiTransportRequest("POST", _prefix + "api/v1/commands/x", "{\"a\":1}", "tok"), CancellationToken.None);

            yield return Wait(call);

            Assert.That(call.Result.StatusCode, Is.EqualTo(200));
            Assert.That(call.Result.Body, Is.EqualTo("{\"ok\":true}"));
            Assert.That(call.Result.NetworkFailureMessage, Is.Null);
            Assert.That(_seen, Is.EqualTo("POST /api/v1/commands/x\nauth=Bearer tok\ntype=application/json\n{\"a\":1}"));
        }

        /// <summary>A GET without token or body sends neither header, and a 422 with a body is an HTTP answer, not a failure.</summary>
        [UnityTest]
        public IEnumerator AGetWithoutTokenOrBody_SendsNeither_AndA422IsAnHttpAnswer()
        {
            StartServer(422, "{\"code\":\"INSUFFICIENT_RESOURCE\"}");
            Task<ApiTransportResponse> call = new UnityWebRequestTransport().SendAsync(
                new ApiTransportRequest("GET", _prefix + "api/v1/city", null, null), CancellationToken.None);

            yield return Wait(call);

            Assert.That(call.Result.StatusCode, Is.EqualTo(422));
            Assert.That(call.Result.Body, Is.EqualTo("{\"code\":\"INSUFFICIENT_RESOURCE\"}"));
            Assert.That(_seen, Does.StartWith("GET /api/v1/city\nauth=\n"));
        }

        /// <summary>The <c>Retry-After</c> header of a 429 comes back as text.</summary>
        [UnityTest]
        public IEnumerator ARetryAfterHeader_ComesBack()
        {
            StartServer(429, "{\"code\":\"RATE_LIMITED\"}", "60");
            Task<ApiTransportResponse> call = new UnityWebRequestTransport().SendAsync(
                new ApiTransportRequest("GET", _prefix + "api/v1/city", null, "tok"), CancellationToken.None);

            yield return Wait(call);

            Assert.That(call.Result.StatusCode, Is.EqualTo(429));
            Assert.That(call.Result.RetryAfterHeader, Is.EqualTo("60"));
        }

        /// <summary>Nothing listens at the address: the result is a network failure with status 0, not an exception.</summary>
        [UnityTest]
        public IEnumerator ARefusedConnection_IsANetworkFailure()
        {
            Task<ApiTransportResponse> call = new UnityWebRequestTransport().SendAsync(
                new ApiTransportRequest("GET", "http://localhost:1/api/v1/city", null, null), CancellationToken.None);

            yield return Wait(call);

            Assert.That(call.Result.NetworkFailureMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(call.Result.StatusCode, Is.EqualTo(0));
        }

        /// <summary>An already cancelled token throws before a request is made.</summary>
        [UnityTest]
        public IEnumerator ACancelledToken_Throws()
        {
            Task<ApiTransportResponse> call = new UnityWebRequestTransport().SendAsync(
                new ApiTransportRequest("GET", "http://localhost:1/api/v1/city", null, null), new CancellationToken(true));

            yield return Wait(call);

            Assert.That(call.IsCanceled || call.IsFaulted, Is.True);
            Assert.That(call.Exception?.InnerException, Is.InstanceOf<OperationCanceledException>().Or.Null);
        }
    }
}
