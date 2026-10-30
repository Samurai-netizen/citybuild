using System.Text.RegularExpressions;

namespace CityBuilder.Simulation.Tests;

/// <summary>
/// Holds the Unity client's error code list (<c>ApiErrorCodes.cs</c> in <c>CityBuilder.Client.Net</c>) to the error table of
/// <c>core-instructions/05-api-contract.md</c>. Only Unity compiles the client, so nothing else would notice a code added to the contract
/// and forgotten in the client (M0-37).
/// </summary>
public sealed partial class ClientErrorCodesTests
{
    /// <summary>The prefix of the codes the client makes up itself, for failures before any server answer exists.</summary>
    private const string _clientPrefix = "CLIENT_";

    /// <summary>The client's list of codes.</summary>
    private static readonly string _codesFile = Path.Combine(RepositoryPaths.Root, "client", "CityBuilder", "Assets", "_Project", "Net", "ApiErrorCodes.cs");

    /// <summary>The contract.</summary>
    private static readonly string _contractFile = Path.Combine(RepositoryPaths.Root, "core-instructions", "05-api-contract.md");

    /// <summary>A row of the contract's error table: the code in backticks, then an HTTP status.</summary>
    [GeneratedRegex(@"^\| `([A-Z_]+)` \| (\d{3}) \|", RegexOptions.Multiline)]
    private static partial Regex TableRow();

    /// <summary>A constant of the client's list: <c>public const string Name = "CODE";</c>.</summary>
    [GeneratedRegex(@"public const string \w+ = ""([A-Z_]+)"";")]
    private static partial Regex Constant();

    /// <summary>The client lists exactly the server codes of the contract's table, in the table's order, and every other code it has is a <c>CLIENT_</c> one.</summary>
    [Fact]
    public void ServerCodes_AreExactlyTheTableOfTheContract()
    {
        string[] table = TableRow().Matches(File.ReadAllText(_contractFile)).Select(match => match.Groups[1].Value).ToArray();
        string[] constants = Constant().Matches(File.ReadAllText(_codesFile)).Select(match => match.Groups[1].Value).ToArray();

        string[] serverCodes = constants.Where(code => !code.StartsWith(_clientPrefix, StringComparison.Ordinal)).ToArray();

        Assert.NotEmpty(table);
        Assert.Equal(table, serverCodes);
    }

    /// <summary>The client-only codes are the three of <c>09</c> (no connection, timeout, an answer that is not the contract's); one more needs its row there and a message.</summary>
    [Fact]
    public void ClientCodes_AreTheThreeFailuresBeforeAnAnswerExists()
    {
        string[] constants = Constant().Matches(File.ReadAllText(_codesFile)).Select(match => match.Groups[1].Value).ToArray();

        string[] clientCodes = constants.Where(code => code.StartsWith(_clientPrefix, StringComparison.Ordinal)).ToArray();

        Assert.Equal(["CLIENT_NETWORK_ERROR", "CLIENT_TIMEOUT", "CLIENT_INVALID_RESPONSE"], clientCodes);
    }
}
