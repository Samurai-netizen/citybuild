using CityBuilder.Infrastructure.Content;
using CityBuilder.Simulation.Content;

namespace CityBuilder.BalanceSim.Tests;

/// <summary>The real content the simulator plays by default, loaded once, and the repository it lives in.</summary>
internal static class TestContent
{
    /// <summary>The repository root, found above the test binaries as the tool finds it.</summary>
    public static string Root { get; } = RepositoryRoot.Find(AppContext.BaseDirectory);

    /// <summary>The content of <c>server/content/mvp-content.json</c>, through the server's strict loader.</summary>
    public static GameContent Mvp { get; } = ContentFileLoader.Load(Path.Combine(Root, "server", "content", "mvp-content.json"));
}

/// <summary>A new empty temporary folder, deleted with everything in it when the test disposes it.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    /// <summary>Creates the folder.</summary>
    public TemporaryFolder() => Path = Directory.CreateTempSubdirectory("balancesim-tests-").FullName;

    /// <summary>The folder's full path.</summary>
    public string Path { get; }

    /// <summary>Deletes the folder.</summary>
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
