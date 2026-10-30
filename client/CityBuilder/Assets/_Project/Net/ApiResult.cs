#nullable enable

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The outcome of one API call: the decoded answer or an <see cref="ApiError"/>, never both. A refusal by a rule, a lost connection
    /// and a timeout are results, not exceptions (<c>14</c> Correctness); only a cancelled call throws.
    /// </summary>
    /// <typeparam name="T">The DTO the endpoint answers with.</typeparam>
    public sealed class ApiResult<T>
        where T : class
    {
        /// <summary>Creates a result; use <see cref="Success"/> or <see cref="Failure"/>.</summary>
        private ApiResult(T? value, ApiError? error)
        {
            Value = value;
            Error = error;
        }

        /// <summary>The decoded answer when <see cref="IsSuccess"/> is true, otherwise null.</summary>
        public T? Value { get; }

        /// <summary>The reason when <see cref="IsSuccess"/> is false, otherwise null.</summary>
        public ApiError? Error { get; }

        /// <summary>Whether the server answered 2xx with a body that decoded.</summary>
        public bool IsSuccess => Error == null;

        /// <summary>A successful result holding <paramref name="value"/>.</summary>
        public static ApiResult<T> Success(T value)
        {
            return new ApiResult<T>(value, null);
        }

        /// <summary>A failed result holding <paramref name="error"/>.</summary>
        public static ApiResult<T> Failure(ApiError error)
        {
            return new ApiResult<T>(null, error);
        }
    }
}
