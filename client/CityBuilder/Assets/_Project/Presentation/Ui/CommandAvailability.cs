#nullable enable

using System;
using CityBuilder.Client.Core;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// Whether the player may give the server a command now, and when not, why (M0-55). It is the one rule behind every button that sends a
    /// command: the stepper's − and +, Upgrade, Finance and Place. A command needs an answer from the server, so only
    /// <see cref="ConnectionState.Connected"/> allows one; every other state switches the buttons off and names its reason on the screen, in the
    /// same words everywhere. It is a hint for the player and nothing more: the server checks every command again, and a command already on its
    /// way (the sender's pending list) is not touched by it. Pure code, so a test checks every state.
    /// </summary>
    public readonly struct CommandAvailability : IEquatable<CommandAvailability>
    {
        /// <summary>The note of the buttons while the client is starting. Commands only exist once connected, so a player sees it for an instant at most.</summary>
        public const string ConnectingReason = "Connecting…";

        /// <summary>The note of the buttons while the server cannot be reached.</summary>
        public const string OfflineReason = "You are offline";

        /// <summary>The note of the buttons while the server answers with a fault of its own.</summary>
        public const string ServerErrorReason = "The server has a problem";

        /// <summary>The note of the buttons while the server does not accept the login.</summary>
        public const string UnauthorizedReason = "Signed out";

        /// <summary>The note of the buttons while this game does not fit the server.</summary>
        public const string IncompatibleVersionReason = "Update needed";

        /// <summary>The reason, or null for the default value, which allows commands.</summary>
        private readonly string? _reason;

        /// <summary>Creates the value; use <see cref="For"/>.</summary>
        private CommandAvailability(string reason)
        {
            _reason = reason;
        }

        /// <summary>Whether a command may be sent.</summary>
        public bool IsAllowed => Reason.Length == 0;

        /// <summary>Why no command may be sent, such as <see cref="OfflineReason"/>; empty when <see cref="IsAllowed"/>.</summary>
        public string Reason => _reason ?? string.Empty;

        /// <summary>The availability that allows a command: the state of a connected client, and the default of a screen that has not heard of the connection.</summary>
        public static CommandAvailability Allowed => new CommandAvailability(string.Empty);

        /// <summary>The availability for how the connection stands.</summary>
        /// <param name="state">The monitor's connection state.</param>
        public static CommandAvailability For(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.Connected:
                    return Allowed;
                case ConnectionState.Connecting:
                    return new CommandAvailability(ConnectingReason);
                case ConnectionState.Offline:
                    return new CommandAvailability(OfflineReason);
                case ConnectionState.Unauthorized:
                    return new CommandAvailability(UnauthorizedReason);
                case ConnectionState.IncompatibleVersion:
                    return new CommandAvailability(IncompatibleVersionReason);
                default:
                    return new CommandAvailability(ServerErrorReason);
            }
        }

        /// <summary>True when both give the same reason (or both allow).</summary>
        public bool Equals(CommandAvailability other)
        {
            return Reason == other.Reason;
        }

        /// <summary>See <see cref="Equals(CommandAvailability)"/>.</summary>
        public override bool Equals(object? obj)
        {
            return obj is CommandAvailability other && Equals(other);
        }

        /// <summary>A hash of the reason, which goes with <see cref="Equals(CommandAvailability)"/>.</summary>
        public override int GetHashCode()
        {
            return Reason.GetHashCode();
        }
    }
}
