#nullable enable

using System.Globalization;
using CityBuilder.Client.Net;

namespace CityBuilder.Client.Presentation.Feedback
{
    /// <summary>
    /// The words of every result (M0-47): one short human sentence for each error code and for each command that succeeded. It is the
    /// only place that turns a code into text for the player (<c>09</c> UX rules), so a screen never shows a code, a server <c>detail</c>
    /// or an exception. The table is a <c>switch</c> over the constants of <see cref="ApiErrorCodes"/>; a test lists those constants by
    /// reflection and fails for a code without a sentence. A sentence that asks the player to wait or try again says so, because a lost
    /// connection or a timeout is not a refusal: the command may still be retried (<c>05</c> Errors). No sentence holds a game number or a
    /// content name, because those change with the content and the era.
    /// </summary>
    public static class FeedbackMessages
    {
        /// <summary>
        /// The code of a command that ended without a server answer to show, or whose flow failed on the client: a client-side code like the
        /// <c>CLIENT_</c> ones of <see cref="ApiErrorCodes"/>, kept here because it never comes from a request (the server-side list test
        /// holds <see cref="ApiErrorCodes"/> to exactly three of them).
        /// </summary>
        public const string CommandFailedCode = "CLIENT_COMMAND_FAILED";

        /// <summary>What a code the table lacks is shown as; no stack, no code, no server text.</summary>
        public const string UnknownErrorMessage = "Something went wrong. Try again";

        /// <summary>The sentence for a failed request: the table's, with the wait of a <c>RATE_LIMITED</c> answer added when the server gave one.</summary>
        /// <param name="error">Why it failed.</param>
        /// <returns>A short sentence for the player; <see cref="UnknownErrorMessage"/> when the code has no entry.</returns>
        public static string ForError(ApiError error)
        {
            if (error.Code == ApiErrorCodes.RateLimited && error.RetryAfterSeconds.HasValue && error.RetryAfterSeconds.Value > 0)
            {
                return "Too many requests. Wait " + error.RetryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture) + " s";
            }

            return TryGetErrorMessage(error.Code, out string message) ? message : UnknownErrorMessage;
        }

        /// <summary>
        /// The table's sentence for <paramref name="code"/>. The completeness test calls this, not <see cref="ForError"/>, so the fallback
        /// cannot hide a missing entry.
        /// </summary>
        /// <param name="code">An error code.</param>
        /// <param name="message">The sentence, or empty when the code has none.</param>
        /// <returns>True when the table has an entry for the code.</returns>
        public static bool TryGetErrorMessage(string code, out string message)
        {
            switch (code)
            {
                case ApiErrorCodes.AuthenticationRequired:
                    message = "Signed out. Log in again";
                    return true;
                case ApiErrorCodes.InvalidArgument:
                    message = "That request was not valid";
                    return true;
                case ApiErrorCodes.UnknownBuildingType:
                    message = "That building does not exist";
                    return true;
                case ApiErrorCodes.BuildingLocked:
                    message = "Research needed first";
                    return true;
                case ApiErrorCodes.OutOfBounds:
                    message = "That spot is outside the city";
                    return true;
                case ApiErrorCodes.TileOccupied:
                    message = "That tile is already taken";
                    return true;
                case ApiErrorCodes.InsufficientResource:
                    message = "Not enough resources";
                    return true;
                case ApiErrorCodes.InsufficientWorkers:
                    message = "Not enough free workers. Build more housing";
                    return true;
                case ApiErrorCodes.WorkerLimit:
                    message = "This building cannot take more workers";
                    return true;
                case ApiErrorCodes.NoFreeBuilder:
                    message = "All builders are busy";
                    return true;
                case ApiErrorCodes.BuildingNotFound:
                    message = "That building is no longer there";
                    return true;
                case ApiErrorCodes.BuildingBusy:
                    message = "That building is still being built or upgraded";
                    return true;
                case ApiErrorCodes.MaxLevel:
                    message = "Already at the highest level";
                    return true;
                case ApiErrorCodes.UnknownResearch:
                    message = "That research does not exist";
                    return true;
                case ApiErrorCodes.ResearchLocked:
                    message = "Finish the earlier research first";
                    return true;
                case ApiErrorCodes.ResearchInProgress:
                    message = "Another research is already running";
                    return true;
                case ApiErrorCodes.ResearchCompleted:
                    message = "Already researched";
                    return true;
                case ApiErrorCodes.IdempotencyConflict:
                    message = "That command was already used. Try again";
                    return true;
                case ApiErrorCodes.RateLimited:
                    message = "Too many requests. Wait a moment";
                    return true;
                case ApiErrorCodes.ConcurrencyConflict:
                    message = "The city was busy. Try again";
                    return true;
                case ApiErrorCodes.InternalError:
                    message = "The server had a problem. Try again later";
                    return true;
                case ApiErrorCodes.NotFound:
                case ApiErrorCodes.MethodNotAllowed:
                case ApiErrorCodes.UnsupportedMediaType:
                    message = "The game and the server do not match. Update the game";
                    return true;
                case ApiErrorCodes.ClientNetworkError:
                    message = "No connection. Check your network and try again";
                    return true;
                case ApiErrorCodes.ClientTimeout:
                    message = "The server did not answer in time. Try again";
                    return true;
                case ApiErrorCodes.ClientInvalidResponse:
                    message = "The server's answer was not understood. Try again later";
                    return true;
                case CommandFailedCode:
                    message = "That did not work. Try again";
                    return true;
                default:
                    message = string.Empty;
                    return false;
            }
        }

        /// <summary>The sentence for a command the server accepted.</summary>
        /// <param name="kind">Which command it was.</param>
        public static string ForSuccess(CommandKind kind)
        {
            switch (kind)
            {
                case CommandKind.PlaceBuilding:
                    return "Construction started";
                case CommandKind.UpgradeBuilding:
                    return "Upgrade started";
                case CommandKind.AssignWorkers:
                    return "Workers updated";
                case CommandKind.StartResearch:
                    return "Research started";
                default:
                    return "Done";
            }
        }
    }
}
