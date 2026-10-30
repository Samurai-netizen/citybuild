using System;
using System.Linq;
using System.Reflection;
using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// <see cref="ApiResponseParser"/> (<c>05</c> Errors): a Problem Details answer becomes an <see cref="ApiError"/>; a 2xx body becomes
    /// its DTO; anything else, a broken body, a proxy's page, a lost connection, becomes a clean client-side error and never an exception.
    /// </summary>
    public sealed class ApiResponseParserTests
    {
        /// <summary>The parser under test.</summary>
        private readonly ApiResponseParser _parser = new ApiResponseParser(new ApiJson());

        /// <summary>Every <c>error-*.json</c> fixture is a real server answer: its code, status, detail and trace id arrive in the error, and its name says which code and status to expect.</summary>
        [Test]
        public void EveryErrorFixture_ParsesToItsCodeAndStatus([ValueSource(nameof(ErrorFixtureNames))] string name)
        {
            string[] parts = name.Substring(0, name.Length - ".json".Length).Split(new[] { '-' }, 3);
            int status = int.Parse(parts[1]);
            string code = parts[2];

            ApiResult<CitySnapshotDto> result = _parser.Parse<CitySnapshotDto>(ApiTransportResponse.Http(status, Fixtures.Read(name), null));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Code, Is.EqualTo(code));
            Assert.That(result.Error.Status, Is.EqualTo(status));
            Assert.That(result.Error.Detail, Is.Not.Empty);
            Assert.That(result.Error.TraceId, Does.StartWith("00-"));
        }

        /// <summary>The names of every <c>error-*.json</c> fixture.</summary>
        public static string[] ErrorFixtureNames()
        {
            return Fixtures.Names("error-");
        }

        /// <summary>A command refused by a rule echoes its request id; a refusal of a request that had none has none.</summary>
        [Test]
        public void TheRequestId_IsReadWhenTheServerEchoedIt()
        {
            ApiError withId = Error(Fixtures.Read("error-422-INSUFFICIENT_RESOURCE.json"), 422);
            ApiError withoutId = Error(Fixtures.Read("error-404-NOT_FOUND.json"), 404);

            Assert.That(withId.RequestId, Is.Not.Null.And.Not.EqualTo(Guid.Empty));
            Assert.That(withoutId.RequestId, Is.Null);
        }

        /// <summary>A rate limit carries the seconds of its <c>Retry-After</c> header.</summary>
        [Test]
        public void RateLimited_CarriesRetryAfterSeconds()
        {
            ApiResult<CitySnapshotDto> result = _parser.Parse<CitySnapshotDto>(ApiTransportResponse.Http(429, Fixtures.Read("error-429-RATE_LIMITED.json"), "60"));

            Assert.That(result.Error.Code, Is.EqualTo(ApiErrorCodes.RateLimited));
            Assert.That(result.Error.RetryAfterSeconds, Is.EqualTo(60));
        }

        /// <summary>A <c>Retry-After</c> that is an HTTP date, not seconds, gives no seconds instead of a wrong number.</summary>
        [TestCase("Wed, 21 Oct 2026 07:28:00 GMT")]
        [TestCase("-5")]
        [TestCase("")]
        public void ARetryAfterThatIsNotWholeSeconds_IsIgnored(string header)
        {
            ApiResult<CitySnapshotDto> result = _parser.Parse<CitySnapshotDto>(ApiTransportResponse.Http(429, Fixtures.Read("error-429-RATE_LIMITED.json"), header));

            Assert.That(result.Error.Code, Is.EqualTo(ApiErrorCodes.RateLimited));
            Assert.That(result.Error.RetryAfterSeconds, Is.Null);
        }

        /// <summary>Every code of the server's catalogue (<c>ApiErrorCodes</c>, held equal to the table of <c>05</c> by a repository test) parses from a body of the server's shape, including the ones no hand run can cause.</summary>
        [Test]
        public void EveryServerCode_ParsesFromAProblemDetailsBody([ValueSource(nameof(ServerCodes))] string code)
        {
            string body = "{\"type\":\"urn:citybuilder:error:x\",\"title\":\"Title\",\"status\":500,\"detail\":\"Detail of " + code + ".\",\"code\":\"" + code + "\",\"traceId\":\"00-abc-def-00\"}";

            ApiError error = Error(body, 500);

            Assert.That(error.Code, Is.EqualTo(code));
            Assert.That(error.Detail, Is.EqualTo("Detail of " + code + "."));
            Assert.That(error.TraceId, Is.EqualTo("00-abc-def-00"));
        }

        /// <summary>The server's codes: every constant of <see cref="ApiErrorCodes"/> that does not start with <c>CLIENT_</c>.</summary>
        public static string[] ServerCodes()
        {
            return typeof(ApiErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => (string)field.GetRawConstantValue())
                .Where(code => !code.StartsWith("CLIENT_", StringComparison.Ordinal))
                .ToArray();
        }

        /// <summary>The server's catalogue has the 24 codes of <c>05</c>; a code added there is added to <see cref="ApiErrorCodes"/> in the same change.</summary>
        [Test]
        public void TheServerCatalogue_HasTwentyFourCodes()
        {
            Assert.That(ServerCodes(), Has.Length.EqualTo(24));
        }

        /// <summary>A 2xx answer becomes its DTO.</summary>
        [Test]
        public void A200Body_BecomesItsDto()
        {
            ApiResult<CitySnapshotDto> result = _parser.Parse<CitySnapshotDto>(ApiTransportResponse.Http(200, Fixtures.Read("snapshot-city-start.json"), null));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Value.City.Inventory["coins"], Is.EqualTo(700L));
        }

        /// <summary>Bodies that must give <c>CLIENT_INVALID_RESPONSE</c> on a 200: broken JSON, cut-off JSON, empty, <c>null</c>, an array, an HTML page, a wrong type, a missing member.</summary>
        [TestCase("{ not json")]
        [TestCase("{\"requestId\":null,\"stateVersion\":2,\"city\":")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("<html><body><h1>200 OK</h1></body></html>")]
        [TestCase("{\"requestId\":null,\"stateVersion\":\"two\"}")]
        [TestCase("{}")]
        public void ASuccessBodyThatIsNotASnapshot_IsAnInvalidResponse(string body)
        {
            ApiResult<CitySnapshotDto> result = _parser.Parse<CitySnapshotDto>(ApiTransportResponse.Http(200, body, null));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(result.Error.Status, Is.EqualTo(200));
            Assert.That(result.Error.TraceId, Is.Null);
        }

        /// <summary>A gateway's answer: HTML, plain text, an empty body or JSON that is no Problem Details is <c>CLIENT_INVALID_RESPONSE</c> with the real HTTP status, and the body is not copied into the error.</summary>
        [TestCase(502, "<html><head><title>502 Bad Gateway</title></head><body>nginx</body></html>")]
        [TestCase(502, "Bad Gateway")]
        [TestCase(503, "")]
        [TestCase(504, "{ \"message\": \"upstream timed out\" }")]
        [TestCase(500, "[1,2,3]")]
        [TestCase(500, "{\"code\":\"\"}")]
        [TestCase(400, "{ broken")]
        [TestCase(400, "null")]
        public void ANonProblemErrorBody_IsAnInvalidResponseWithTheHttpStatus(int status, string body)
        {
            ApiError error = Error(body, status);

            Assert.That(error.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(error.Status, Is.EqualTo(status));
            Assert.That(error.Detail, Does.Not.Contain("nginx").And.Not.Contain("upstream"));
        }

        /// <summary>A lost connection is <c>CLIENT_NETWORK_ERROR</c> with status 0.</summary>
        [Test]
        public void ANetworkFailure_IsAClientNetworkError()
        {
            ApiResult<CitySnapshotDto> result = _parser.Parse<CitySnapshotDto>(ApiTransportResponse.NetworkFailure("Cannot connect to destination host"));

            Assert.That(result.Error.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));
            Assert.That(result.Error.Status, Is.EqualTo(0));
            Assert.That(result.Error.Detail, Does.Contain("Cannot connect"));
        }

        /// <summary>A Problem Details body without a detail falls back to its title; one without both has an empty detail, never null.</summary>
        [Test]
        public void TheDetail_FallsBackToTheTitle_AndIsNeverNull()
        {
            ApiError withTitle = Error("{\"code\":\"NOT_FOUND\",\"title\":\"Not found\"}", 404);
            ApiError bare = Error("{\"code\":\"NOT_FOUND\"}", 404);

            Assert.That(withTitle.Detail, Is.EqualTo("Not found"));
            Assert.That(bare.Detail, Is.EqualTo(string.Empty));
        }

        /// <summary>Parses <paramref name="body"/> as an error answer with <paramref name="status"/> and returns its error.</summary>
        private ApiError Error(string body, int status)
        {
            ApiResult<CitySnapshotDto> result = _parser.Parse<CitySnapshotDto>(ApiTransportResponse.Http(status, body, null));
            Assert.That(result.IsSuccess, Is.False);
            return result.Error;
        }
    }
}
