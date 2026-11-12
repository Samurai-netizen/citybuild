namespace CityBuilder.DocTools.Tests;

/// <summary>The generated pages: deterministic, marked as generated, and the committed ones current (docs/modules/DocTools.md).</summary>
public sealed class ReferenceDocsTests
{
    /// <summary>Two runs over the same repository give the same pages, byte for byte, in the same order.</summary>
    [Fact]
    public void Generate_TwiceOnTheSameRepository_GivesIdenticalPages()
    {
        IReadOnlyList<GeneratedPage> first = ReferenceDocs.Generate(TestRepository.Root);
        IReadOnlyList<GeneratedPage> second = ReferenceDocs.Generate(TestRepository.Root);

        Assert.Equal(first, second);
    }

    /// <summary>Two writes into different folders give files with identical bytes.</summary>
    [Fact]
    public void Write_IntoTwoFolders_GivesIdenticalFiles()
    {
        using var first = new TemporaryFolder();
        using var second = new TemporaryFolder();

        IReadOnlyList<string> names = ReferenceDocs.Write(TestRepository.Root, first.Path);
        ReferenceDocs.Write(TestRepository.Root, second.Path);

        Assert.NotEmpty(names);
        foreach (string name in names)
        {
            Assert.Equal(File.ReadAllBytes(System.IO.Path.Combine(first.Path, name)), File.ReadAllBytes(System.IO.Path.Combine(second.Path, name)));
        }
    }

    /// <summary>Every page starts with the notice that it is generated, uses only <c>\n</c> line ends and names no absolute path of this machine.</summary>
    [Fact]
    public void Generate_EveryPage_StartsWithTheNoticeAndHoldsNoMachineDetails()
    {
        foreach (GeneratedPage page in ReferenceDocs.Generate(TestRepository.Root))
        {
            Assert.StartsWith(MarkdownPage.Notice + "\n", page.Text, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', page.Text);
            Assert.DoesNotContain(TestRepository.Root, page.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>A stale page in the output folder, one no generator makes any more, is deleted by the next write.</summary>
    [Fact]
    public void Write_FolderWithAStalePage_DeletesIt()
    {
        using var folder = new TemporaryFolder();
        string stale = System.IO.Path.Combine(folder.Path, "old-page.md");
        File.WriteAllText(stale, "old");

        ReferenceDocs.Write(TestRepository.Root, folder.Path);

        Assert.False(File.Exists(stale));
    }

    /// <summary>
    /// The committed pages in <c>docs/reference/generated/</c> are what the code generates now, as CI's step "Generated docs are current"
    /// checks. When this fails, run <c>dotnet run --project tools/DocTools -- generate</c> and commit the result.
    /// </summary>
    [Fact]
    public void CommittedPages_AreCurrent()
    {
        using var fresh = new TemporaryFolder();
        ReferenceDocs.Write(TestRepository.Root, fresh.Path);

        IReadOnlyList<PageDifference> differences = GeneratedDocsCheck.Compare(ReferenceDocs.OutputFolder(TestRepository.Root), fresh.Path);

        Assert.True(differences.Count == 0, string.Join("\n", differences.Select(difference => difference.Describe())));
    }
}
