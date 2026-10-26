namespace CityBuilder.Repository.Tests.Documentation;

/// <summary>
/// <see cref="SourceFiles"/> on the real repository: it finds the code of every area, skips build output and the exempt template folder,
/// and lists the files in a stable order. The floors make a broken search fail loudly instead of checking nothing.
/// </summary>
public sealed class SourceFilesTests
{
    /// <summary>The list holds a known file of each area (engine, server, server tests, Unity client) and at least 150 files in all.</summary>
    [Fact]
    public void All_Repository_FindsTheCodeOfEveryArea()
    {
        IReadOnlyList<string> files = SourceFiles.All(RepositoryPaths.Root);

        Assert.Contains("shared/Simulation/Runtime/IntegerMath.cs", files);
        Assert.Contains("server/src/CityBuilder.Api/Program.cs", files);
        Assert.Contains("server/tests/CityBuilder.Repository.Tests/Documentation/DocumentationRules.cs", files);
        Assert.Contains("client/CityBuilder/Assets/_Project/Core/AppInfo.cs", files);
        Assert.True(files.Count >= 150, $"Only {files.Count} C# files found; the search must be broken.");
    }

    /// <summary>No listed file lies in build output, an IDE or Unity cache, or the Unity template's exempt sample folder.</summary>
    [Fact]
    public void All_Repository_SkipsBuildOutputCachesAndExemptFolders()
    {
        IReadOnlyList<string> files = SourceFiles.All(RepositoryPaths.Root);

        Assert.DoesNotContain(files, file => file.Contains("/bin/", StringComparison.Ordinal) || file.Contains("/obj/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, file => file.Contains("/Library/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, file => file.StartsWith("client/CityBuilder/Assets/TutorialInfo/", StringComparison.Ordinal));
    }

    /// <summary>The files come in ordinal order, so reports are identical on every machine and in every run.</summary>
    [Fact]
    public void All_Repository_ListsFilesInOrdinalOrder()
    {
        IReadOnlyList<string> files = SourceFiles.All(RepositoryPaths.Root);

        Assert.Equal(files.Order(StringComparer.Ordinal), files);
    }
}
