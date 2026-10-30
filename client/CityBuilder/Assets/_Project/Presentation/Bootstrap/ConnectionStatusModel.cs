#nullable enable

using System.Collections.Generic;
using System.Globalization;
using CityBuilder.Client.Net;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;

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

        /// <summary><c>Connected - Early Middle Ages - Coins: 700</c>; the era and item names are the content's, a missing one falls back to the id or number.</summary>
        private static string ConnectedText(GameContent content, CitySnapshotDto city)
        {
            string era = content.TryGetEra(city.City.Era, out EraDefinition? eraDefinition)
                ? eraDefinition.Name
                : "Era " + city.City.Era.ToString(CultureInfo.InvariantCulture);
            string coinsName = content.TryGetItem(new ItemId(CoinsItemId), out ItemDefinition? coins) ? coins.Name : CoinsItemId;
            city.City.Inventory.TryGetValue(CoinsItemId, out long amount);
            return "Connected - " + era + " - " + coinsName + ": " + amount.ToString(CultureInfo.InvariantCulture);
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
