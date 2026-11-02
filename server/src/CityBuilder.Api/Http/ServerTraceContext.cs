using System.Diagnostics;

namespace CityBuilder.Api.Http;

/// <summary>
/// The server's trace context rule (core-instructions/07-security-model.md, M0-60): every request's trace id is the server's own. ASP.NET Core
/// reads the W3C <c>traceparent</c>, <c>tracestate</c> and <c>baggage</c> headers by default and makes the request's activity a child of what
/// they name, so a client could choose the trace id the server logs and answers with (<c>traceId</c> in Problem Details), reuse another
/// request's id to muddle a search, or put text into the activity's baggage. This propagator reads none of them and writes none: the server
/// calls no other service, so there is nothing to pass on. Registered in <c>Program.cs</c> in place of the default.
/// </summary>
public sealed class ServerTraceContext : DistributedContextPropagator
{
    /// <summary>The header names this propagator reads or writes: none.</summary>
    public override IReadOnlyCollection<string> Fields { get; } = [];

    /// <summary>Writes nothing into an outgoing carrier.</summary>
    /// <param name="activity">The activity whose context would be passed on.</param>
    /// <param name="carrier">The outgoing request.</param>
    /// <param name="setter">Sets one header on the carrier.</param>
    public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter)
    {
    }

    /// <summary>Reads no trace id or trace state from the request, so the request's activity starts a new trace.</summary>
    /// <param name="carrier">The incoming request.</param>
    /// <param name="getter">Reads one header from the carrier.</param>
    /// <param name="traceId">Always null.</param>
    /// <param name="traceState">Always null.</param>
    public override void ExtractTraceIdAndState(object? carrier, PropagatorGetterCallback? getter, out string? traceId, out string? traceState)
    {
        traceId = null;
        traceState = null;
    }

    /// <summary>Reads no baggage from the request.</summary>
    /// <param name="carrier">The incoming request.</param>
    /// <param name="getter">Reads one header from the carrier.</param>
    /// <returns>Always null.</returns>
    public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(object? carrier, PropagatorGetterCallback? getter) => null;
}
