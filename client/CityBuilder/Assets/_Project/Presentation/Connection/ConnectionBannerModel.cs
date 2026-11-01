#nullable enable

using System;
using CityBuilder.Client.Core;

namespace CityBuilder.Client.Presentation.Connection
{
    /// <summary>
    /// What the connection banner and the blocking screen say (M0-55), as plain text and flags. While the client is <see cref="ConnectionState.Offline"/>,
    /// has a <see cref="ConnectionState.ServerError"/> or is <see cref="ConnectionState.Unauthorized"/> the city stays on screen, predicted from the
    /// last snapshot, and the banner says why it may be out of date, how long ago it was last synced, and offers one action (try again, or sign in
    /// again). A client that is <see cref="ConnectionState.IncompatibleVersion"/> cannot trust anything it would show, so the blocking screen covers the
    /// city with a message. The model reads only the state, the age of the last snapshot and whether a retry is running; it decides nothing, and the
    /// action's effect (a poll, which includes the one new login) is the flow's.
    /// </summary>
    public sealed class ConnectionBannerModel : IEquatable<ConnectionBannerModel>
    {
        /// <summary>The banner's line while the server cannot be reached.</summary>
        public const string OfflineMessage = "Offline. The city shown may be out of date.";

        /// <summary>The banner's line while the server answers with a fault of its own.</summary>
        public const string ServerErrorMessage = "The server has a problem. The city shown may be out of date.";

        /// <summary>The banner's line while the server does not accept the login.</summary>
        public const string UnauthorizedMessage = "Signed out. The server did not accept the login.";

        /// <summary>The title of the blocking screen.</summary>
        public const string BlockingTitle = "Update needed";

        /// <summary>The message of the blocking screen: what is wrong and what the player can do.</summary>
        public const string BlockingMessage = "This version of the game does not match the server. Install the latest version of the game, then open it again.";

        /// <summary>The action while the server is down or faulty.</summary>
        public const string TryAgainText = "Try again";

        /// <summary>The action while the login is refused.</summary>
        public const string SignInAgainText = "Sign in again";

        /// <summary>The action of the blocking screen: the server may have been changed back, so it is worth asking once more.</summary>
        public const string CheckAgainText = "Check again";

        /// <summary>The action's text while a retry is running; the button is off then.</summary>
        public const string TryingText = "Trying…";

        /// <summary>Creates a model; use <see cref="Create"/>.</summary>
        private ConnectionBannerModel()
        {
        }

        /// <summary>Whether the banner is shown: the city is on screen but the connection is not good.</summary>
        public bool IsBannerShown { get; private set; }

        /// <summary>Whether the blocking screen is shown: nothing the client has is usable with this server.</summary>
        public bool IsBlocking { get; private set; }

        /// <summary>The banner's line, or empty when the banner is not shown.</summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>When the city shown was last synced, such as <c>Last synced 3m ago</c>; empty when the banner is not shown.</summary>
        public string SyncedText { get; private set; } = string.Empty;

        /// <summary>The action's button text (<see cref="TryAgainText"/>, <see cref="SignInAgainText"/>, <see cref="CheckAgainText"/>, or <see cref="TryingText"/>); empty when nothing is shown.</summary>
        public string ActionText { get; private set; } = string.Empty;

        /// <summary>Whether the action's button works: false while a retry is running.</summary>
        public bool ActionEnabled { get; private set; }

        /// <summary>The blocking screen's title, or empty when it is not shown.</summary>
        public string BlockingTitleText { get; private set; } = string.Empty;

        /// <summary>The blocking screen's message, or empty when it is not shown.</summary>
        public string BlockingMessageText { get; private set; } = string.Empty;

        /// <summary>Whether anything is shown.</summary>
        public bool IsAnythingShown => IsBannerShown || IsBlocking;

        /// <summary>Makes the words for how the connection stands.</summary>
        /// <param name="state">The monitor's connection state.</param>
        /// <param name="secondsSinceSync">The whole seconds since the last snapshot arrived (<c>ClientCityStore.WholeSecondsSinceReceived</c>).</param>
        /// <param name="isRetrying">Whether the player pressed the action and the poll it asked for has not ended.</param>
        public static ConnectionBannerModel Create(ConnectionState state, long secondsSinceSync, bool isRetrying)
        {
            var model = new ConnectionBannerModel();
            switch (state)
            {
                case ConnectionState.Offline:
                    model.ShowBanner(OfflineMessage, TryAgainText, secondsSinceSync, isRetrying);
                    break;
                case ConnectionState.ServerError:
                    model.ShowBanner(ServerErrorMessage, TryAgainText, secondsSinceSync, isRetrying);
                    break;
                case ConnectionState.Unauthorized:
                    model.ShowBanner(UnauthorizedMessage, SignInAgainText, secondsSinceSync, isRetrying);
                    break;
                case ConnectionState.IncompatibleVersion:
                    model.IsBlocking = true;
                    model.BlockingTitleText = BlockingTitle;
                    model.BlockingMessageText = BlockingMessage;
                    model.ActionText = isRetrying ? TryingText : CheckAgainText;
                    model.ActionEnabled = !isRetrying;
                    break;
            }

            return model;
        }

        /// <summary>True when every text and flag is the same: the screen has nothing to redraw.</summary>
        public bool Equals(ConnectionBannerModel? other)
        {
            return other != null
                && IsBannerShown == other.IsBannerShown
                && IsBlocking == other.IsBlocking
                && Message == other.Message
                && SyncedText == other.SyncedText
                && ActionText == other.ActionText
                && ActionEnabled == other.ActionEnabled
                && BlockingTitleText == other.BlockingTitleText
                && BlockingMessageText == other.BlockingMessageText;
        }

        /// <summary>See <see cref="Equals(ConnectionBannerModel?)"/>.</summary>
        public override bool Equals(object? obj) => Equals(obj as ConnectionBannerModel);

        /// <summary>A hash of the texts that change most, which goes with <see cref="Equals(ConnectionBannerModel?)"/>.</summary>
        public override int GetHashCode() => HashCode.Combine(IsBannerShown, IsBlocking, Message, SyncedText, ActionText);

        /// <summary>Fills the banner's members.</summary>
        private void ShowBanner(string message, string action, long secondsSinceSync, bool isRetrying)
        {
            IsBannerShown = true;
            Message = message;
            SyncedText = "Last synced " + SyncAgeText.Format(Math.Max(0, secondsSinceSync));
            ActionText = isRetrying ? TryingText : action;
            ActionEnabled = !isRetrying;
        }
    }
}
