using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
    /// <c>Retry-After</c>, a redirect, a refused connection, a cancelled call and a cancel from a timer's thread while the server stays silent. The tests are coroutines because Unity completes web requests on its own update loop.
    /// </summary>
    public sealed class UnityWebRequestTransportTests
    {
        /// <summary>The server of the current test.</summary>
        private HttpListener _listener;

        /// <summary>The request the server last saw: method, path, headers and body.</summary>
        private volatile string _seen;

        /// <summary>The prefix the server listens on, for example <c>http://localhost:49213/</c>.</summary>
        private string _prefix;

        /// <summary>The socket of the test that needs a server that takes connections and never answers.</summary>
        private TcpListener _silent;

        /// <summary>Starts a server that answers every request with <paramref name="status"/>, <paramref name="body"/> and an optional <c>Retry-After</c>.</summary>
        private void StartServer(int status, string body, string retryAfter = null, string location = null)
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

            Task.Run(() => Serve(status, body, retryAfter, location));
        }

        /// <summary>Answers requests until the listener stops.</summary>
        private void Serve(int status, string body, string retryAfter, string location)
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
                _seen += context.Request.HttpMethod + " " + context.Request.Url.AbsolutePath + "\n"
                    + "auth=" + context.Request.Headers["Authorization"] + "\n"
                    + "type=" + context.Request.ContentType + "\n" + requestBody;
                context.Response.StatusCode = status;
                if (location != null)
                {
                    context.Response.AddHeader("Location", location);
                }

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
            _silent?.Stop();
            _silent = null;
            _seen = null;
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

        /// <summary>
        /// The server takes the connection and never answers, and a timer cancels the token, as <see cref="ApiClient"/>'s timeout does. A timer
        /// cancels on another thread, so <c>UnityWebRequest.Abort</c> runs there; measured in the Editor, that ends the request without an error
        /// (CR-0004 decision 9). If it stopped doing so, the request would never end, and with it the poll or the retries that wait for it.
        /// </summary>
        [UnityTest]
        public IEnumerator ACancelFromATimer_EndsARequestTheServerNeverAnswers()
        {
            _silent = new TcpListener(IPAddress.Loopback, 0);
            _silent.Start();
            int port = ((IPEndPoint)_silent.LocalEndpoint).Port;
            using (var timeout = new CancellationTokenSource())
            {
                Task<ApiTransportResponse> call = new UnityWebRequestTransport().SendAsync(
                    new ApiTransportRequest("GET", "http://127.0.0.1:" + port + "/api/v1/city", null, "tok"), timeout.Token);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(200));

                yield return Wait(call);

                Assert.That(call.IsCanceled, Is.True, "the cancelled request should end as cancelled, but is " + call.Status);
            }
        }

        /// <summary>
        /// A cancel on the main thread, as when the scene is destroyed, aborts at once: the call ends without waiting for another frame's turn.
        /// </summary>
        [UnityTest]
        public IEnumerator ACancelOnTheMainThread_EndsTheRequest()
        {
            _silent = new TcpListener(IPAddress.Loopback, 0);
            _silent.Start();
            int port = ((IPEndPoint)_silent.LocalEndpoint).Port;
            using (var cancel = new CancellationTokenSource())
            {
                Task<ApiTransportResponse> call = new UnityWebRequestTransport().SendAsync(
                    new ApiTransportRequest("GET", "http://127.0.0.1:" + port + "/api/v1/city", null, "tok"), cancel.Token);

                yield return null;
                cancel.Cancel();
                yield return Wait(call);

                Assert.That(call.IsCanceled, Is.True, "the cancelled request should end as cancelled, but is " + call.Status);
            }
        }

        /// <summary>
        /// A redirect is not followed: the API never sends one, and following it would hand the bearer token to wherever a captive portal or a
        /// proxy points, and turn a POST into a GET (CR-0004). The server sees the first request only, and the client gets a failure, not the other page.
        /// </summary>
        [UnityTest]
        public IEnumerator ARedirect_IsNotFollowed()
        {
            StartServer(302, string.Empty, null, "/elsewhere");
            Task<ApiTransportResponse> call = new UnityWebRequestTransport().SendAsync(
                new ApiTransportRequest("POST", _prefix + "api/v1/commands/x", "{\"a\":1}", "tok"), CancellationToken.None);

            yield return Wait(call);

            Assert.That(_seen, Does.Not.Contain("/elsewhere"));
            Assert.That(call.Result.StatusCode == 200, Is.False, "a redirect must not end as a success");
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
