using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// <see cref="ApiClient"/> over a <see cref="FakeTransport"/>: the request it builds for each endpoint of <c>05</c> (method, URL,
    /// body, bearer token), the result it returns, and its timeout and cancellation. The wire itself is tested in
    /// <see cref="UnityWebRequestTransportTests"/>.
    /// </summary>
    public sealed class ApiClientTests
    {
        /// <summary>The address every client of these tests uses.</summary>
        private const string BaseUrl = "http://localhost:8080";

        /// <summary>A request id used by the command tests.</summary>
        private static readonly Guid RequestId = new Guid("6f1c0000-0000-4000-8000-000000000001");

        /// <summary>A client with a 15 second timeout over <paramref name="transport"/>.</summary>
        private static ApiClient NewClient(FakeTransport transport, string baseUrl = BaseUrl, int timeoutSeconds = 15)
        {
            return new ApiClient(baseUrl, timeoutSeconds, transport, allowInsecureHttp: true);
        }

        /// <summary>Every request, the login included and with or without a token, carries the build version for <c>X-Client-Version</c> (M0-58).</summary>
        [Test]
        public async Task EveryRequest_CarriesTheClientVersion()
        {
            FakeTransport transport = new FakeTransport()
                .Answer(200, "{\"accessToken\":\"a.b.c\",\"expiresAtUtc\":\"2026-10-27T21:00:00+00:00\"}")
                .Answer(200, Fixtures.Read("content.json"))
                .Answer(200, Fixtures.Read("snapshot-city-start.json"));
            ApiClient client = new ApiClient(BaseUrl, 15, transport, null, "1.2.3", allowInsecureHttp: true);

            await client.DevLoginAsync("devtest", CancellationToken.None);
            client.SetAccessToken("tok");
            await client.GetContentAsync(CancellationToken.None);
            await client.GetCityAsync(CancellationToken.None);

            Assert.That(transport.Requests, Has.Count.EqualTo(3));
            Assert.That(transport.Requests.ConvertAll(sent => sent.ClientVersion), Is.All.EqualTo("1.2.3"));
        }

        /// <summary>Without a version given, the client sends the build's own (<c>AppInfo.Version</c>, the Player setting), which is not blank.</summary>
        [Test]
        public async Task Request_WithoutAGivenVersion_CarriesTheBuildsVersion()
        {
            FakeTransport transport = new FakeTransport().Answer(200, Fixtures.Read("snapshot-city-start.json"));
            ApiClient client = NewClient(transport);

            await client.GetCityAsync(CancellationToken.None);

            Assert.That(transport.Requests[0].ClientVersion, Is.EqualTo(CityBuilder.Client.Core.AppInfo.Version));
            Assert.That(transport.Requests[0].ClientVersion, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>A blank version sends no header at all rather than an empty one.</summary>
        [TestCase("")]
        [TestCase("   ")]
        public async Task Request_WithABlankVersion_SendsNoHeader(string version)
        {
            FakeTransport transport = new FakeTransport().Answer(200, Fixtures.Read("snapshot-city-start.json"));
            ApiClient client = new ApiClient(BaseUrl, 15, transport, null, version, allowInsecureHttp: true);

            await client.GetCityAsync(CancellationToken.None);

            Assert.That(transport.Requests[0].ClientVersion, Is.Null);
        }

        /// <summary>A base URL that is not an absolute http(s) address, or a timeout outside 1 to 300 seconds, is refused at construction.</summary>
        [TestCase("")]
        [TestCase("localhost:8080")]
        [TestCase("/relative")]
        [TestCase("ftp://localhost")]
        public void ABadBaseUrl_IsRefused(string baseUrl)
        {
            Assert.That(() => new ApiClient(baseUrl, 15, new FakeTransport(), allowInsecureHttp: true), Throws.ArgumentException);
        }

        /// <summary>
        /// Plain http is refused unless the caller allows it (<c>07</c>, M0-60): a release build, which <c>GameRoot</c> does not allow it,
        /// never sends its bearer token unencrypted; the default is off.
        /// </summary>
        [TestCase("http://localhost:8080")]
        [TestCase("http://192.168.1.20:8080")]
        [TestCase("HTTP://example.com")]
        public void AnHttpBaseUrl_WithoutTheAllowance_IsRefused(string baseUrl)
        {
            Assert.That(() => new ApiClient(baseUrl, 15, new FakeTransport()), Throws.ArgumentException.With.Message.Contains("https"));
            Assert.That(() => new ApiClient(baseUrl, 15, new FakeTransport(), allowInsecureHttp: false), Throws.ArgumentException);
        }

        /// <summary>The same http address is accepted when allowed, as in the Editor and a development build.</summary>
        [Test]
        public void AnHttpBaseUrl_WithTheAllowance_BuildsAClient()
        {
            Assert.That(() => new ApiClient("http://localhost:8080", 15, new FakeTransport(), allowInsecureHttp: true), Throws.Nothing);
        }

        /// <summary>An https address is accepted in every build, and its requests go to https.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AnHttpsBaseUrl_IsAcceptedInEveryBuild(bool allowInsecureHttp)
        {
            FakeTransport transport = new FakeTransport().Answer(200, Fixtures.Read("snapshot-city-start.json"));
            ApiClient client = new ApiClient("https://api.example.com", 15, transport, allowInsecureHttp: allowInsecureHttp);

            await client.GetCityAsync(CancellationToken.None);

            Assert.That(transport.Requests[0].Url, Is.EqualTo("https://api.example.com/api/v1/city"));
        }

        /// <summary>A timeout of 0 or over 300 seconds is refused.</summary>
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(301)]
        public void ABadTimeout_IsRefused(int seconds)
        {
            Assert.That(() => new ApiClient(BaseUrl, seconds, new FakeTransport(), allowInsecureHttp: true), Throws.ArgumentException);
        }

        /// <summary><c>GET /city</c> is a GET without a body that carries the token, to <c>/api/v1/city</c>, also when the base URL ends in a slash.</summary>
        [TestCase(BaseUrl)]
        [TestCase(BaseUrl + "/")]
        public async Task GetCity_SendsAGetWithTheToken(string baseUrl)
        {
            FakeTransport transport = new FakeTransport().Answer(200, Fixtures.Read("snapshot-city-start.json"));
            ApiClient client = NewClient(transport, baseUrl);
            client.SetAccessToken("tok");

            ApiResult<CitySnapshotDto> result = await client.GetCityAsync(CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.City.Era, Is.EqualTo(1));
            ApiTransportRequest sent = transport.Requests[0];
            Assert.That(sent.Method, Is.EqualTo("GET"));
            Assert.That(sent.Url, Is.EqualTo("http://localhost:8080/api/v1/city"));
            Assert.That(sent.JsonBody, Is.Null);
            Assert.That(sent.AccessToken, Is.EqualTo("tok"));
        }

        /// <summary><c>GET /content</c> reads the content fixture.</summary>
        [Test]
        public async Task GetContent_ReadsTheContent()
        {
            FakeTransport transport = new FakeTransport().Answer(200, Fixtures.Read("content.json"));
            ApiClient client = NewClient(transport);

            ApiResult<ContentResponseDto> result = await client.GetContentAsync(CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.ContentVersion, Is.EqualTo("mvp-1"));
            Assert.That(transport.Requests[0].Url, Is.EqualTo("http://localhost:8080/api/v1/content"));
        }

        /// <summary>The dev login never sends a token, even when one is set, and reads the answer.</summary>
        [Test]
        public async Task DevLogin_SendsNoToken_AndReadsTheAnswer()
        {
            FakeTransport transport = new FakeTransport().Answer(200, "{\"accessToken\":\"a.b.c\",\"expiresAtUtc\":\"2026-10-27T21:00:00+00:00\"}");
            ApiClient client = NewClient(transport);
            client.SetAccessToken("old");

            ApiResult<DevLoginResponseDto> result = await client.DevLoginAsync("devtest", CancellationToken.None);

            Assert.That(result.Value.AccessToken, Is.EqualTo("a.b.c"));
            ApiTransportRequest sent = transport.Requests[0];
            Assert.That(sent.Method, Is.EqualTo("POST"));
            Assert.That(sent.Url, Is.EqualTo("http://localhost:8080/api/v1/auth/dev-login"));
            Assert.That(sent.JsonBody, Is.EqualTo("{\"displayName\":\"devtest\"}"));
            Assert.That(sent.AccessToken, Is.Null);
        }

        /// <summary>Clearing the token with null makes later requests go out without one (the server then answers 401).</summary>
        [Test]
        public async Task ClearingTheToken_SendsNoToken()
        {
            FakeTransport transport = new FakeTransport().Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"));
            ApiClient client = NewClient(transport);
            client.SetAccessToken("tok");
            client.SetAccessToken(null);

            ApiResult<CitySnapshotDto> result = await client.GetCityAsync(CancellationToken.None);

            Assert.That(transport.Requests[0].AccessToken, Is.Null);
            Assert.That(result.Error.Code, Is.EqualTo(ApiErrorCodes.AuthenticationRequired));
        }

        /// <summary>Each command is a POST to its own path with exactly its body and the token.</summary>
        [Test]
        public async Task EachCommand_PostsItsBodyToItsPath()
        {
            string snapshot = Fixtures.Read("snapshot-command-place-building.json");
            FakeTransport transport = new FakeTransport().Answer(200, snapshot).Answer(200, snapshot).Answer(200, snapshot).Answer(200, snapshot);
            ApiClient client = NewClient(transport);
            client.SetAccessToken("tok");

            await client.PlaceBuildingAsync(new PlaceBuildingRequestDto(RequestId, "house", 1, 2), CancellationToken.None);
            await client.UpgradeBuildingAsync(new UpgradeBuildingRequestDto(RequestId, 3), CancellationToken.None);
            await client.AssignWorkersAsync(new AssignWorkersRequestDto(RequestId, 3, 2), CancellationToken.None);
            await client.StartResearchAsync(new StartResearchRequestDto(RequestId, "trade_charter"), CancellationToken.None);

            string[] paths = { "place-building", "upgrade-building", "assign-workers", "start-research" };
            for (int i = 0; i < paths.Length; i++)
            {
                ApiTransportRequest sent = transport.Requests[i];
                Assert.That(sent.Method, Is.EqualTo("POST"));
                Assert.That(sent.Url, Is.EqualTo("http://localhost:8080/api/v1/commands/" + paths[i]));
                Assert.That(sent.AccessToken, Is.EqualTo("tok"));
                Assert.That(sent.JsonBody, Does.StartWith("{\"requestId\":\"" + RequestId + "\","));
            }

            Assert.That(transport.Requests[2].JsonBody, Is.EqualTo("{\"requestId\":\"6f1c0000-0000-4000-8000-000000000001\",\"buildingId\":3,\"workers\":2}"));
        }

        /// <summary>A rule's refusal is a failed result with the server's code, not an exception.</summary>
        [Test]
        public async Task ARefusal_IsAFailedResult()
        {
            FakeTransport transport = new FakeTransport().Answer(422, Fixtures.Read("error-422-INSUFFICIENT_RESOURCE.json"));
            ApiClient client = NewClient(transport);

            ApiResult<CitySnapshotDto> result = await client.PlaceBuildingAsync(new PlaceBuildingRequestDto(RequestId, "sawmill", 3, 0), CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ApiErrorCodes.InsufficientResource));
            Assert.That(result.Error.Status, Is.EqualTo(422));
        }

        /// <summary>A 502 page from a proxy and a lost connection both come back as results the caller can retry on.</summary>
        [Test]
        public async Task AGatewayPage_AndALostConnection_AreResults()
        {
            FakeTransport transport = new FakeTransport().Answer(502, "<html>Bad Gateway</html>").Fail("Cannot connect to destination host");
            ApiClient client = NewClient(transport);

            ApiResult<CitySnapshotDto> gateway = await client.GetCityAsync(CancellationToken.None);
            ApiResult<CitySnapshotDto> lost = await client.GetCityAsync(CancellationToken.None);

            Assert.That(gateway.Error.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(gateway.Error.Status, Is.EqualTo(502));
            Assert.That(lost.Error.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));
            Assert.That(lost.Error.Status, Is.EqualTo(0));
        }

        /// <summary>A server that never answers gives <c>CLIENT_TIMEOUT</c> after the client's timeout, with status 0.</summary>
        [Test]
        public async Task AServerThatNeverAnswers_GivesAClientTimeout()
        {
            FakeTransport transport = new FakeTransport().NeverAnswer();
            ApiClient client = NewClient(transport, timeoutSeconds: 1);

            ApiResult<CitySnapshotDto> result = await client.GetCityAsync(CancellationToken.None);

            Assert.That(result.Error.Code, Is.EqualTo(ApiErrorCodes.ClientTimeout));
            Assert.That(result.Error.Status, Is.EqualTo(0));
        }

        /// <summary>Cancelling the caller's token while a request waits throws; it is not reported as a timeout.</summary>
        [Test]
        public void CancellingTheCallersToken_ThrowsInsteadOfTimingOut()
        {
            FakeTransport transport = new FakeTransport().NeverAnswer();
            ApiClient client = NewClient(transport);
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            {
                Task<ApiResult<CitySnapshotDto>> call = client.GetCityAsync(cancel.Token);
                cancel.Cancel();

                Assert.That(async () => await call, Throws.InstanceOf<OperationCanceledException>());
            }
        }

        /// <summary>A token that is already cancelled throws before anything is sent.</summary>
        [Test]
        public void ACancelledToken_SendsNothing()
        {
            FakeTransport transport = new FakeTransport();
            ApiClient client = NewClient(transport);

            Assert.That(async () => await client.GetCityAsync(new CancellationToken(true)), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(transport.Requests, Is.Empty);
        }
    }
}
