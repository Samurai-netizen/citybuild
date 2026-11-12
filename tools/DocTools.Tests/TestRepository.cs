namespace CityBuilder.DocTools.Tests;

/// <summary>The real repository the tests generate from, and temporary folders that clean up after themselves.</summary>
internal static class TestRepository
{
    /// <summary>The repository root, found above the test binaries as the tool finds it.</summary>
    public static string Root { get; } = RepositoryRoot.Find(AppContext.BaseDirectory);
}

/// <summary>A new empty temporary folder, deleted with everything in it when the test disposes it.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    /// <summary>Creates the folder.</summary>
    public TemporaryFolder() => Path = Directory.CreateTempSubdirectory("doctools-tests-").FullName;

    /// <summary>The folder's full path.</summary>
    public string Path { get; }

    /// <summary>Deletes the folder.</summary>
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
