namespace CityBuilder.DocTools.Tests;

/// <summary>The <c>check</c> command finds every committed page that is not current, and passes when all are.</summary>
public sealed class GeneratedDocsCheckTests
{
    /// <summary>Committed pages equal to fresh ones pass the check with exit code 0 and nothing on the error output.</summary>
    [Fact]
    public void Run_CommittedPagesCurrent_Succeeds()
    {
        using var committed = new TemporaryFolder();
        ReferenceDocs.Write(TestRepository.Root, committed.Path);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = GeneratedDocsCheck.Run(TestRepository.Root, committed.Path, output, error);

        Assert.Equal(CommandLine.Success, exitCode);
        Assert.Empty(error.ToString());
    }

    /// <summary>A committed page edited by hand fails the check with exit code 1, and the report names the page and the line.</summary>
    [Fact]
    public void Run_ModifiedCommittedPage_FailsAndNamesThePage()
    {
        using var committed = new TemporaryFolder();
        string name = ReferenceDocs.Write(TestRepository.Root, committed.Path)[0];
        string path = System.IO.Path.Combine(committed.Path, name);
        string[] lines = File.ReadAllText(path).Split('\n');
        lines[2] = "# Edited by hand";
        File.WriteAllText(path, string.Join('\n', lines));
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = GeneratedDocsCheck.Run(TestRepository.Root, committed.Path, output, error);

        Assert.Equal(CommandLine.PagesOutOfDate, exitCode);
        Assert.Contains($"changed  {name}: 1 line(s) differ, first at line 3", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("committed: # Edited by hand", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A page the generator makes but that isn't committed is reported as missing.</summary>
    [Fact]
    public void Compare_PageNotCommitted_ReportsMissing()
    {
        using var committed = new TemporaryFolder();
        using var fresh = new TemporaryFolder();
        string name = ReferenceDocs.Write(TestRepository.Root, fresh.Path)[0];
        ReferenceDocs.Write(TestRepository.Root, committed.Path);
        File.Delete(System.IO.Path.Combine(committed.Path, name));

        PageDifference difference = Assert.Single(GeneratedDocsCheck.Compare(committed.Path, fresh.Path));

        Assert.Equal(new PageDifference(name, DifferenceKind.Missing, 0, string.Empty, string.Empty, 0), difference);
    }

    /// <summary>A committed page that no generator makes any more is reported as extra.</summary>
    [Fact]
    public void Compare_PageNoLongerGenerated_ReportsExtra()
    {
        using var committed = new TemporaryFolder();
        using var fresh = new TemporaryFolder();
        ReferenceDocs.Write(TestRepository.Root, fresh.Path);
        ReferenceDocs.Write(TestRepository.Root, committed.Path);
        File.WriteAllText(System.IO.Path.Combine(committed.Path, "removed.md"), "text");

        PageDifference difference = Assert.Single(GeneratedDocsCheck.Compare(committed.Path, fresh.Path));

        Assert.Equal(DifferenceKind.Extra, difference.Kind);
        Assert.Equal("removed.md", difference.FileName);
    }

    /// <summary>A page that only gained lines at its end reports the first added line and counts every added one.</summary>
    [Fact]
    public void CompareTexts_LinesAddedAtTheEnd_ReportsTheFirstAddedLine()
    {
        PageDifference? difference = GeneratedDocsCheck.CompareTexts("page.md", "a\nb\n", "a\nb\nc\nd\n");

        Assert.Equal(new PageDifference("page.md", DifferenceKind.Changed, 3, string.Empty, "c", 2), difference);
    }

    /// <summary>Equal texts give no difference.</summary>
    [Fact]
    public void CompareTexts_EqualTexts_GivesNull() => Assert.Null(GeneratedDocsCheck.CompareTexts("page.md", "a\n", "a\n"));
}
