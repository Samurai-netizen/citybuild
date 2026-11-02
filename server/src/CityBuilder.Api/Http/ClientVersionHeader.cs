namespace CityBuilder.Api.Http;

/// <summary>
/// Reads the client's build version from the <c>X-Client-Version</c> header (<c>05</c> Versioning) and puts it in the log scope of the request as
/// <c>ClientVersion</c>, so every line the request writes (the settlement, the command, a refusal, an exception) names the build that sent it.
/// Nothing is refused over it: rejecting unsupported versions is a later task. The value is client-controlled text that goes into the log, so only a
/// short run of plain characters is kept; anything else is logged as <see cref="Invalid"/>, never as the text itself (<c>07</c> Logging policy).
/// </summary>
public static class ClientVersionHeader
{
    /// <summary>The name of the request header.</summary>
    public const string HeaderName = "X-Client-Version";

    /// <summary>The name of the log scope value.</summary>
    public const string ScopeName = "ClientVersion";

    /// <summary>What the log says when the request has no <c>X-Client-Version</c> header (a tool, a test, an old build).</summary>
    public const string None = "none";

    /// <summary>What the log says when the header is there but is not a version: empty, repeated, too long, or with other characters than the allowed ones.</summary>
    public const string Invalid = "invalid";

    /// <summary>The most characters a version may have.</summary>
    public const int MaxLength = 32;

    /// <summary>The value a log line gets for a header.</summary>
    /// <param name="header">The values of the header; empty when the request has none.</param>
    /// <returns>The version itself when it is one (letters, digits, <c>.</c>, <c>-</c>, <c>+</c>, <c>_</c>; 1 to <see cref="MaxLength"/> characters),
    /// <see cref="None"/> when the header is absent, otherwise <see cref="Invalid"/>.</returns>
    public static string ForLog(Microsoft.Extensions.Primitives.StringValues header)
    {
        if (header.Count == 0)
        {
            return None;
        }

        string? value = header.Count == 1 ? header[0] : null;
        return value is { Length: >= 1 and <= MaxLength } && value.All(IsAllowed) ? value : Invalid;
    }

    /// <summary>Adds the scope step at the start of the pipeline, after the response headers and before the exception handler, so the handler's own lines have it too.</summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static IApplicationBuilder UseClientVersionLogScope(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ILogger logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ClientVersionHeader).FullName!);
        return app.Use(async (context, next) =>
        {
            string version = ForLog(context.Request.Headers[HeaderName]);
            using IDisposable? scope = logger.BeginScope(new KeyValuePair<string, object>[] { new(ScopeName, version) });
            await next(context);
        });
    }

    /// <summary>Whether a character may be in a version.</summary>
    /// <param name="c">The character.</param>
    /// <returns>True for an ASCII letter or digit and for <c>.</c>, <c>-</c>, <c>+</c> and <c>_</c>.</returns>
    private static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+' or '_';
}
