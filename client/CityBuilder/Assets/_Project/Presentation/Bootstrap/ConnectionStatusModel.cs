#nullable enable

using System.Collections.Generic;
using System.Globalization;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Bootstrap
{
    /// <summary>
    /// What the start screen says: a one-line headline for the status label and, for a fatal error, the lines under it. The view model of
    /// <see cref="GameRoot"/>'s status label, with no engine objects, so a test checks every sentence. The messages here cover the failures
    /// of starting up; the full table of error codes to messages is M0-47 (<c>09</c> UX rules).
    /// </summary>
    public sealed class ConnectionStatusModel
    {
        /// <summary>The most content errors listed on the fatal screen; the rest are counted.</summary>
        private const int MaxListedErrors = 8;

        /// <summary>The content id of the coins item: the one currency the start screen names (<c>23</c>). It is an id, not a game number.</summary>
        public const string CoinsItemId = "coins";

        /// <summary>Creates a model; use the static members.</summary>
        private ConnectionStatusModel(string headline, IReadOnlyList<string> details, bool isFatal)
        {
            Headline = headline;
            Details = details;
            IsFatal = isFatal;
        }

        /// <summary>The label text.</summary>
        public string Headline { get; }

        /// <summary>Lines shown under the headline of a fatal screen; empty otherwise.</summary>
        public IReadOnlyList<string> Details { get; }

        /// <summary>Whether the fatal error screen is shown instead of the status label: nothing the player does can fix it.</summary>
        public bool IsFatal { get; }

        /// <summary>Shown from the start until the flow ends.</summary>
        public static ConnectionStatusModel Connecting()
        {
            return new ConnectionStatusModel("Connecting...", new string[0], false);
        }

        /// <summary>The client could not be built from its settings asset (a wrong URL or timeout): a configuration error, fatal.</summary>
        public static ConnectionStatusModel BadSettings(string reason)
        {
            return new ConnectionStatusModel("Cannot start", new[] { "The API client settings are wrong: " + reason }, true);
        }

        /// <summary>The status for how the flow ended.</summary>
        public static ConnectionStatusModel From(StartupOutcome outcome)
        {
            switch (outcome.Kind)
            {
                case StartupResultKind.Connected:
                    return new ConnectionStatusModel(ConnectedText(outcome.Content!, outcome.City!), new string[0], false);
                case StartupResultKind.InvalidContent:
                    return new ConnectionStatusModel("The game content from the server is invalid", ContentErrorLines(outcome.ContentErrors), true);
                default:
                    return new ConnectionStatusModel(FailedText(outcome.Error!), new string[0], false);
            }
        }

        /// <summary>
        /// The status for the connection as the polling sees it (M0-41), shown whenever <see cref="ConnectionMonitor.State"/> changes:
        /// <c>Connected - ...</c> as at the start, but from the latest snapshot, or one sentence that names what is wrong, ending in the code
        /// of <paramref name="error"/> when there is one.
        /// </summary>
        /// <param name="state">The connection state.</param>
        /// <param name="error">Why it is not connected; null when connected or when no error is the reason.</param>
        /// <param name="content">The content the era and item names come from.</param>
        /// <param name="snapshot">The latest snapshot; used only for <see cref="ConnectionState.Connected"/>, and null falls back to the era and coins of an empty city.</param>
        public static ConnectionStatusModel ForConnection(ConnectionState state, ApiError? error, GameContent content, CitySnapshot? snapshot)
        {
            string code = error == null ? string.Empty : " (" + error.Code + ")";
            switch (state)
            {
                case ConnectionState.Connecting:
                    return Connecting();
                case ConnectionState.Connected:
                    CityState? city = snapshot?.CopyCity();
                    int era = city == null ? 0 : city.Era;
                    long coins = city == null ? 0 : city.GetQuantity(new ItemId(CoinsItemId));
                    return new ConnectionStatusModel(ConnectedText(content, era, coins), new string[0], false);
                case ConnectionState.Offline:
                    string reason = error != null && error.Code == ApiErrorCodes.ClientTimeout ? "the server did not answer in time" : "cannot reach the server";
                    return new ConnectionStatusModel("Offline: " + reason + code, new string[0], false);
                case ConnectionState.Unauthorized:
                    return new ConnectionStatusModel("Signed out: the server did not accept the login" + code, new string[0], false);
                case ConnectionState.IncompatibleVersion:
                    return new ConnectionStatusModel("Update needed: this game does not match the server" + (error == null ? " (other content version)" : code), new string[0], false);
                default:
                    return new ConnectionStatusModel("Server problem: the server could not be used" + code, new string[0], false);
            }
        }

        /// <summary><c>Connected - Early Middle Ages - Coins: 700</c> from the first answer's DTO.</summary>
        private static string ConnectedText(GameContent content, CitySnapshotDto city)
        {
            city.City.Inventory.TryGetValue(CoinsItemId, out long amount);
            return ConnectedText(content, city.City.Era, amount);
        }

        /// <summary><c>Connected - Early Middle Ages - Coins: 700</c>; the era and item names are the content's, a missing one falls back to the id or number.</summary>
        private static string ConnectedText(GameContent content, int eraNumber, long coinsAmount)
        {
            string era = content.TryGetEra(eraNumber, out EraDefinition? eraDefinition)
                ? eraDefinition.Name
                : "Era " + eraNumber.ToString(CultureInfo.InvariantCulture);
            string coinsName = content.TryGetItem(new ItemId(CoinsItemId), out ItemDefinition? coins) ? coins.Name : CoinsItemId;
            return "Connected - " + era + " - " + coinsName + ": " + coinsAmount.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A sentence for a failed request; the code is added so a bug report names it.</summary>
        private static string FailedText(ApiError error)
        {
            string text;
            switch (error.Code)
            {
                case ApiErrorCodes.ClientNetworkError:
                    text = "Cannot reach the server";
                    break;
                case ApiErrorCodes.ClientTimeout:
                    text = "The server did not answer in time";
                    break;
                case ApiErrorCodes.ClientInvalidResponse:
                    text = "The server's answer was not understood";
                    break;
                case ApiErrorCodes.NotFound:
                    text = "The server has no dev login (it is not a Development server)";
                    break;
                case ApiErrorCodes.InvalidArgument:
                    text = "The server refused the dev player name";
                    break;
                case ApiErrorCodes.RateLimited:
                    text = "Too many requests, try again in a moment";
                    break;
                default:
                    text = "The server could not be used";
                    break;
            }

            return "Not connected: " + text + " (" + error.Code + ")";
        }

        /// <summary>One line per content error (at most <see cref="MaxListedErrors"/>), then a count of the rest.</summary>
        private static IReadOnlyList<string> ContentErrorLines(IReadOnlyList<ContentError> errors)
        {
            var lines = new List<string>();
            for (int i = 0; i < errors.Count && i < MaxListedErrors; i++)
            {
                lines.Add(errors[i].Path + ": " + errors[i].Message);
            }

            if (errors.Count > MaxListedErrors)
            {
                lines.Add("... and " + (errors.Count - MaxListedErrors).ToString(CultureInfo.InvariantCulture) + " more");
            }

            return lines;
        }
    }
}
