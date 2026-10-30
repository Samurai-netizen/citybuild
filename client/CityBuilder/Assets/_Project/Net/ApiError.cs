#nullable enable

using System;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Why a request did not give the client what it asked for: the server's Problem Details answer (<c>05</c> Errors) or a failure
    /// before one existed (no connection, a timeout, an answer that is not what <c>05</c> says). Programs branch on <see cref="Code"/>
    /// (<see cref="ApiErrorCodes"/>); <see cref="Detail"/> is for logs and may change. Views show a message looked up by code, never
    /// this text (<c>09</c> UX rules).
    /// </summary>
    public sealed class ApiError
    {
        /// <summary>Creates an error. Use the members of <see cref="ApiErrorCodes"/> for <paramref name="code"/>.</summary>
        /// <param name="code">The stable error code.</param>
        /// <param name="status">The HTTP status, or 0 when no HTTP answer arrived.</param>
        /// <param name="detail">A human sentence for logs; never null.</param>
        /// <param name="traceId">The server's W3C trace id of the request, or null when the answer had none.</param>
        /// <param name="requestId">The command's request id the server echoed, or null.</param>
        /// <param name="retryAfterSeconds">The whole seconds of a <c>Retry-After</c> header, or null.</param>
        public ApiError(string code, int status, string detail, string? traceId, Guid? requestId, int? retryAfterSeconds)
        {
            Code = code;
            Status = status;
            Detail = detail;
            TraceId = traceId;
            RequestId = requestId;
            RetryAfterSeconds = retryAfterSeconds;
        }

        /// <summary>The stable error code: a server code of <c>05</c> or a <c>CLIENT_</c> code (<see cref="ApiErrorCodes"/>).</summary>
        public string Code { get; }

        /// <summary>The HTTP status of the answer; 0 when there was none (<c>CLIENT_NETWORK_ERROR</c>, <c>CLIENT_TIMEOUT</c>).</summary>
        public int Status { get; }

        /// <summary>A human sentence for logs. It may change between versions and is never matched in code.</summary>
        public string Detail { get; }

        /// <summary>The server's trace id of the HTTP request (<c>docs/api/logs-and-correlation.md</c>); quote it in a bug report.</summary>
        public string? TraceId { get; }

        /// <summary>The command's request id when the server echoed it (a command refused by a rule); null otherwise.</summary>
        public Guid? RequestId { get; }

        /// <summary>The seconds to wait before retrying, from the <c>Retry-After</c> header of a <c>RATE_LIMITED</c> answer; null otherwise.</summary>
        public int? RetryAfterSeconds { get; }

        /// <summary>A one-line form for logs: code, status, detail and trace id.</summary>
        public override string ToString()
        {
            return Code + " (" + Status + "): " + Detail + (TraceId == null ? string.Empty : " [trace " + TraceId + "]");
        }
    }
}
