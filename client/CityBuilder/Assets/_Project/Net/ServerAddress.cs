#nullable enable

using System;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Picks the server address a player talks to (M0-82, <c>docs/BUILD.md</c>): the settings asset's, unless the player was started with
    /// <c>-server-url &lt;address&gt;</c> (or <c>-server-url=&lt;address&gt;</c>). So one build reaches another server without a code or
    /// asset change. The address is still checked by <see cref="ApiClient"/>: https only, except in development builds.
    /// </summary>
    public static class ServerAddress
    {
        /// <summary>The command-line option that replaces the asset's address.</summary>
        public const string Option = "-server-url";

        /// <summary>
        /// The address to use: the option's value when it is given, else <paramref name="assetUrl"/>. The option without a value gives
        /// an empty address, which <see cref="ApiClient"/> refuses, so a mistyped launch shows the settings error.
        /// </summary>
        /// <param name="commandLine">The player's command line (<c>Environment.GetCommandLineArgs()</c>); null when the platform has none.</param>
        /// <param name="assetUrl">The address of the settings asset.</param>
        /// <returns>The address to use.</returns>
        public static string Resolve(string[]? commandLine, string assetUrl)
        {
            if (commandLine == null)
            {
                return assetUrl;
            }

            for (int i = 0; i < commandLine.Length; i++)
            {
                string argument = commandLine[i];
                if (argument == Option)
                {
                    return i + 1 < commandLine.Length ? commandLine[i + 1] : string.Empty;
                }

                if (argument.StartsWith(Option + "=", StringComparison.Ordinal))
                {
                    return argument.Substring(Option.Length + 1);
                }
            }

            return assetUrl;
        }
    }
}
