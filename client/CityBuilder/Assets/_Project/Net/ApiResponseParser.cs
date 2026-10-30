#nullable enable

using System;
using System.Globalization;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Turns what the wire returned into an <see cref="ApiResult{T}"/>: a 2xx body into its DTO, anything else into an
    /// <see cref="ApiError"/> (<c>05</c> Errors). It never throws on a bad body: a broken, empty, HTML or wrongly shaped body from a
    /// proxy or a newer server is <c>CLIENT_INVALID_RESPONSE</c>, and the body itself is never copied into the error.
    /// </summary>
    public sealed class ApiResponseParser
    {
        /// <summary>The reader of every body.</summary>
        private readonly ApiJson _json;

        /// <summary>Creates a parser that reads with <paramref name="json"/>.</summary>
        public ApiResponseParser(ApiJson json)
        {
            _json = json;
        }

        /// <summary>Reads one response.</summary>
        /// <typeparam name="T">The DTO a 2xx answer carries.</typeparam>
        /// <param name="response">What the transport returned.</param>
        public ApiResult<T> Parse<T>(ApiTransportResponse response)
            where T : class
        {
            if (response.NetworkFailureMessage != null)
            {
                return Fail<T>(ApiErrorCodes.ClientNetworkError, 0, "No answer from the server: " + response.NetworkFailureMessage);
            }

            bool isSuccess = response.StatusCode >= 200 && response.StatusCode < 300;
            return isSuccess ? ParseSuccess<T>(response) : ParseFailure<T>(response);
        }

        /// <summary>Decodes a 2xx body into <typeparamref name="T"/>.</summary>
        private ApiResult<T> ParseSuccess<T>(ApiTransportResponse response)
            where T : class
        {
            try
            {
                T? value = _json.Deserialize<T>(response.Body);
                if (value == null)
                {
                    return Fail<T>(ApiErrorCodes.ClientInvalidResponse, response.StatusCode, "The answer has no content.");
                }

                return ApiResult<T>.Success(value);
            }
            catch (JsonException exception)
            {
                // The body is not echoed: only the reader's own message, which names the member that failed.
                return Fail<T>(ApiErrorCodes.ClientInvalidResponse, response.StatusCode, "The answer is not what the API contract says: " + exception.Message);
            }
        }

        /// <summary>Reads a non-2xx answer as Problem Details, or as an invalid response when it is not one.</summary>
        private ApiResult<T> ParseFailure<T>(ApiTransportResponse response)
            where T : class
        {
            ProblemDetailsDto? problem = TryReadProblem(response.Body);
            string? code = problem?.Code;
            if (problem == null || code == null || code.Length == 0)
            {
                return Fail<T>(ApiErrorCodes.ClientInvalidResponse, response.StatusCode, "The server answered " + response.StatusCode + " without a Problem Details body.");
            }

            string detail = problem.Detail ?? problem.Title ?? string.Empty;
            return ApiResult<T>.Failure(new ApiError(code, response.StatusCode, detail, problem.TraceId, ReadGuid(problem.RequestId), ReadSeconds(response.RetryAfterHeader)));
        }

        /// <summary>Reads <paramref name="body"/> as Problem Details; null when it is empty, not JSON or not an object.</summary>
        private ProblemDetailsDto? TryReadProblem(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                return _json.Deserialize<ProblemDetailsDto>(body);
            }
            catch (JsonException)
            {
                // A proxy's HTML page or a cut-off body: the caller reports it as an invalid response.
                return null;
            }
        }

        /// <summary>Reads a request id the server echoed; null when absent or not a GUID.</summary>
        private static Guid? ReadGuid(string? text)
        {
            return text != null && Guid.TryParse(text, out Guid value) ? value : (Guid?)null;
        }

        /// <summary>Reads a <c>Retry-After</c> header given in whole seconds; null for an absent header or an HTTP date.</summary>
        private static int? ReadSeconds(string? header)
        {
            bool parsed = int.TryParse(header, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds);
            return parsed ? seconds : (int?)null;
        }

        /// <summary>A failed result for a failure that happened on the client side or in reading the answer.</summary>
        private static ApiResult<T> Fail<T>(string code, int status, string detail)
            where T : class
        {
            return ApiResult<T>.Failure(new ApiError(code, status, detail, null, null, null));
        }
    }
}
