#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// The auth state machine (M0-38): <see cref="AuthState.LoggedOut"/>, <see cref="AuthState.LoggingIn"/>, <see cref="AuthState.LoggedIn"/>
    /// and <see cref="AuthState.Failed"/>, the token on the client, and what is kept for the next run. The wire is a <see cref="FakeTransport"/>.
    /// </summary>
    public sealed class AuthServiceTests
    {
        /// <summary>The login answer of a Development server (<c>05</c> Authentication).</summary>
        private const string LoginAnswer = "{\"accessToken\":\"tok-1\",\"expiresAtUtc\":\"2026-10-27T21:00:00+00:00\"}";

        /// <summary>A refusal body of the server's shape (<c>400 INVALID_ARGUMENT</c>).</summary>
        private const string InvalidArgumentAnswer = "{\"status\":400,\"code\":\"INVALID_ARGUMENT\",\"detail\":\"The name is not allowed.\",\"traceId\":\"00-abc-def-00\"}";

        /// <summary>The fake wire of the current test.</summary>
        private FakeTransport _transport = null!;

        /// <summary>The client under the service.</summary>
        private ApiClient _api = null!;

        /// <summary>The store the service writes to.</summary>
        private MemoryAccessTokenStore _store = null!;

        /// <summary>The service under test.</summary>
        private AuthService _auth = null!;

        /// <summary>Before each test: a logged-out service over a fake wire and an empty store.</summary>
        [SetUp]
        public void CreateService()
        {
            _transport = new FakeTransport();
            _api = new ApiClient("http://localhost:8080", 15, _transport);
            _store = new MemoryAccessTokenStore();
            _auth = new AuthService(_api, _store);
        }

        /// <summary>
        /// Something unexpected during a login (here the fake wire, which throws for a request the test queued no answer for) leaves the service
        /// logged out. Before, it stayed <c>LoggingIn</c>, and every later login and <c>Forget</c> threw "already in progress" for good (CR-0004).
        /// </summary>
        [Test]
        public async Task Login_UnexpectedException_LeavesTheServiceLoggedOut_AndTheNextLoginWorks()
        {
            try
            {
                await _auth.LoginAsync("dev1", CancellationToken.None);
                Assert.Fail("The login should have thrown.");
            }
            catch (InvalidOperationException)
            {
                // What the test expects: the exception reaches the caller.
            }

            Assert.That(_auth.State, Is.EqualTo(AuthState.LoggedOut));
            Assert.DoesNotThrow(() => _auth.Forget());
            _transport.Answer(200, LoginAnswer);
            Assert.That(await _auth.LoginAsync("dev1", CancellationToken.None), Is.EqualTo(AuthState.LoggedIn));
        }

        /// <summary>A new service has no token and no error.</summary>
        [Test]
        public void NewService_IsLoggedOut_WithoutAnError()
        {
            Assert.That(_auth.State, Is.EqualTo(AuthState.LoggedOut));
            Assert.That(_auth.LastError, Is.Null);
        }

        /// <summary>A good answer logs in: the name goes to the dev login, the token goes to the client and to the store.</summary>
        [Test]
        public async Task Login_WithAGoodAnswer_IsLoggedIn_AndHandsTheTokenOn()
        {
            _transport.Answer(200, LoginAnswer).Answer(200, "{}");

            AuthState state = await _auth.LoginAsync("dev1", CancellationToken.None);

            Assert.That(state, Is.EqualTo(AuthState.LoggedIn));
            Assert.That(_auth.State, Is.EqualTo(AuthState.LoggedIn));
            Assert.That(_auth.LastError, Is.Null);
            Assert.That(_transport.Requests[0].Method, Is.EqualTo("POST"));
            Assert.That(_transport.Requests[0].Url, Is.EqualTo("http://localhost:8080/api/v1/auth/dev-login"));
            Assert.That(_transport.Requests[0].JsonBody, Is.EqualTo("{\"displayName\":\"dev1\"}"));
            Assert.That(_transport.Requests[0].AccessToken, Is.Null);
            Assert.That(_store.Kept!.DisplayName, Is.EqualTo("dev1"));
            Assert.That(_store.Kept.AccessToken, Is.EqualTo("tok-1"));
            await _api.GetCityAsync(CancellationToken.None);
            Assert.That(_transport.Requests[1].AccessToken, Is.EqualTo("tok-1"));
        }

        /// <summary>The state while the request is on its way is <see cref="AuthState.LoggingIn"/>.</summary>
        [Test]
        public void Login_WhileTheRequestIsOpen_IsLoggingIn()
        {
            _transport.NeverAnswer();
            using (var cancel = new CancellationTokenSource())
            {
                Task<AuthState> login = _auth.LoginAsync("dev1", cancel.Token);

                Assert.That(_auth.State, Is.EqualTo(AuthState.LoggingIn));
                cancel.Cancel();
                Assert.That(async () => await login, Throws.InstanceOf<OperationCanceledException>());
            }
        }

        /// <summary>A second login (or restore, or forget) during a login is a programming error: two would race for the token.</summary>
        [Test]
        public void Login_WhileAnotherLoginIsOpen_Throws()
        {
            _transport.NeverAnswer();
            using (var cancel = new CancellationTokenSource())
            {
                Task<AuthState> first = _auth.LoginAsync("dev1", cancel.Token);

                Assert.That(() => _auth.LoginAsync("dev1", CancellationToken.None), Throws.InvalidOperationException);
                Assert.That(() => _auth.TryRestore("dev1"), Throws.InvalidOperationException);
                Assert.That(() => _auth.Forget(), Throws.InvalidOperationException);
                cancel.Cancel();
                Assert.That(async () => await first, Throws.InstanceOf<OperationCanceledException>());
            }
        }

        /// <summary>A cancelled login leaves the service logged out, with no token anywhere, and throws.</summary>
        [Test]
        public void Login_Cancelled_ThrowsAndIsLoggedOut()
        {
            _transport.NeverAnswer();
            using (var cancel = new CancellationTokenSource())
            {
                Task<AuthState> login = _auth.LoginAsync("dev1", cancel.Token);
                cancel.Cancel();

                Assert.That(async () => await login, Throws.InstanceOf<OperationCanceledException>());
            }

            Assert.That(_auth.State, Is.EqualTo(AuthState.LoggedOut));
            Assert.That(_store.Kept, Is.Null);
        }

        /// <summary>A refusal fails the login with the server's code and keeps no token.</summary>
        [Test]
        public async Task Login_Refused_IsFailed_WithTheServersCode()
        {
            _transport.Answer(400, InvalidArgumentAnswer);

            AuthState state = await _auth.LoginAsync("bad name", CancellationToken.None);

            Assert.That(state, Is.EqualTo(AuthState.Failed));
            Assert.That(_auth.LastError!.Code, Is.EqualTo(ApiErrorCodes.InvalidArgument));
            Assert.That(_auth.LastError.Status, Is.EqualTo(400));
            Assert.That(_store.Kept, Is.Null);
        }

        /// <summary>A lost connection fails the login with <c>CLIENT_NETWORK_ERROR</c>.</summary>
        [Test]
        public async Task Login_WithoutAConnection_IsFailed_WithTheClientNetworkCode()
        {
            _transport.Fail("Cannot connect to destination host");

            AuthState state = await _auth.LoginAsync("dev1", CancellationToken.None);

            Assert.That(state, Is.EqualTo(AuthState.Failed));
            Assert.That(_auth.LastError!.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));
        }

        /// <summary>An answer that holds an empty token is a broken answer, not a login.</summary>
        [Test]
        public async Task Login_WithAnEmptyToken_IsFailed_AsAnInvalidResponse()
        {
            _transport.Answer(200, "{\"accessToken\":\"\",\"expiresAtUtc\":\"2026-10-27T21:00:00+00:00\"}");

            AuthState state = await _auth.LoginAsync("dev1", CancellationToken.None);

            Assert.That(state, Is.EqualTo(AuthState.Failed));
            Assert.That(_auth.LastError!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(_store.Kept, Is.Null);
        }

        /// <summary>A failed login after a good one drops the old token: the client is not left holding a token it was told to replace.</summary>
        [Test]
        public async Task Login_FailingAfterASuccess_DropsTheOldToken()
        {
            _transport.Answer(200, LoginAnswer).Answer(400, InvalidArgumentAnswer).Answer(401, "{}");
            await _auth.LoginAsync("dev1", CancellationToken.None);

            await _auth.LoginAsync("dev2", CancellationToken.None);
            await _api.GetCityAsync(CancellationToken.None);

            Assert.That(_auth.State, Is.EqualTo(AuthState.Failed));
            Assert.That(_store.Kept, Is.Null);
            Assert.That(_transport.Requests[2].AccessToken, Is.Null);
        }

        /// <summary>A good login after a failed one clears the error.</summary>
        [Test]
        public async Task Login_SucceedingAfterAFailure_ClearsTheError()
        {
            _transport.Fail("down").Answer(200, LoginAnswer);
            await _auth.LoginAsync("dev1", CancellationToken.None);

            await _auth.LoginAsync("dev1", CancellationToken.None);

            Assert.That(_auth.State, Is.EqualTo(AuthState.LoggedIn));
            Assert.That(_auth.LastError, Is.Null);
        }

        /// <summary>An empty name never reaches the wire.</summary>
        [Test]
        public void Login_WithAnEmptyName_Throws_AndSendsNothing()
        {
            Assert.That(() => _auth.LoginAsync(string.Empty, CancellationToken.None), Throws.ArgumentException);
            Assert.That(_transport.Requests, Is.Empty);
        }

        /// <summary>A kept login for the same name is taken without asking the server: logged in, token on the client.</summary>
        [Test]
        public async Task TryRestore_WithAKeptLoginForTheName_IsLoggedIn_WithoutARequest()
        {
            _store.Kept = new StoredLogin("dev1", "kept-token");
            _transport.Answer(200, "{}");

            bool restored = _auth.TryRestore("dev1");
            await _api.GetCityAsync(CancellationToken.None);

            Assert.That(restored, Is.True);
            Assert.That(_auth.State, Is.EqualTo(AuthState.LoggedIn));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_transport.Requests[0].AccessToken, Is.EqualTo("kept-token"));
        }

        /// <summary>A kept login for another name is not used: another name is another player.</summary>
        [Test]
        public void TryRestore_WithAKeptLoginForAnotherName_StaysLoggedOut()
        {
            _store.Kept = new StoredLogin("dev1", "kept-token");

            bool restored = _auth.TryRestore("dev2");

            Assert.That(restored, Is.False);
            Assert.That(_auth.State, Is.EqualTo(AuthState.LoggedOut));
        }

        /// <summary>With nothing kept there is nothing to restore.</summary>
        [Test]
        public void TryRestore_WithNothingKept_StaysLoggedOut()
        {
            Assert.That(_auth.TryRestore("dev1"), Is.False);
            Assert.That(_auth.State, Is.EqualTo(AuthState.LoggedOut));
        }

        /// <summary>Forgetting drops the token from the client and the store, so the next request goes out without one.</summary>
        [Test]
        public async Task Forget_DropsTheTokenEverywhere()
        {
            _store.Kept = new StoredLogin("dev1", "kept-token");
            _auth.TryRestore("dev1");
            _transport.Answer(401, "{}");

            _auth.Forget();
            await _api.GetCityAsync(CancellationToken.None);

            Assert.That(_auth.State, Is.EqualTo(AuthState.LoggedOut));
            Assert.That(_store.Kept, Is.Null);
            Assert.That(_transport.Requests[0].AccessToken, Is.Null);
        }

        /// <summary>Nothing the service keeps is a city: the stored login has exactly a name and a token.</summary>
        [Test]
        public void StoredLogin_RefusesEmptyParts()
        {
            Assert.That(() => new StoredLogin(string.Empty, "t"), Throws.ArgumentException);
            Assert.That(() => new StoredLogin("dev1", string.Empty), Throws.ArgumentException);
        }
    }
}
