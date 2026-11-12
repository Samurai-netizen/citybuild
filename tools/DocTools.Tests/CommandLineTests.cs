namespace CityBuilder.DocTools.Tests;

/// <summary>The command line: unknown commands and arguments get the usage text and exit code 2.</summary>
public sealed class CommandLineTests
{
    /// <summary>A command DocTools doesn't have, no command, or a stray argument prints the usage and exits with <see cref="CommandLine.Usage"/>.</summary>
    /// <param name="args">The command line, split at spaces.</param>
    [Theory]
    [InlineData("")]
    [InlineData("publish")]
    [InlineData("generate --verbose")]
    public void Run_UnknownCommandLine_PrintsUsage(string args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = CommandLine.Run(args.Split(' ', StringSplitOptions.RemoveEmptyEntries), output, error);

        Assert.Equal(CommandLine.Usage, exitCode);
        Assert.StartsWith("Usage:", error.ToString(), StringComparison.Ordinal);
    }
}
