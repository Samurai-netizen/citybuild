namespace CityBuilder.Api.OpenApi;

/// <summary>
/// The build-time OpenAPI document (M0-61): every build of the API writes <c>obj/openapi/CityBuilder.Api.json</c>
/// (<c>Microsoft.Extensions.ApiDescription.Server</c>), which <c>tools/DocTools</c> turns into <c>docs/reference/generated/endpoints.md</c>.
/// To write it, the package's tool runs <c>Program.cs</c> in its own process up to <c>app.Run()</c> and then stops it: the host never
/// listens and never serves a request. That run has no secrets and no database, so it gets the placeholder settings below, and it runs as
/// Development so the document also holds the Development-only dev login.
/// </summary>
public static class BuildTimeDocument
{
    /// <summary>The entry assembly of the package's tool while it runs the app to read the document.</summary>
    private const string _toolAssemblyName = "GetDocument.Insider";

    /// <summary>Whether this process is the build-time document run, not a server.</summary>
    public static bool IsRunning { get; } =
        string.Equals(System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name, _toolAssemblyName, StringComparison.Ordinal);

    /// <summary>
    /// The settings the run starts with instead of the real ones: a connection string that names no reachable server (nothing connects),
    /// no migrations, and a signing key that only gets the startup check past; it signs nothing, because the run serves no request.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["ConnectionStrings:CityBuilder"] = "Host=build-time-openapi-document.invalid;Database=citybuilder",
        ["Database:ApplyMigrationsAtStartup"] = "false",
        ["Auth:SigningKey"] = "build-time-openapi-document-run-only-signs-nothing",
    };
}
