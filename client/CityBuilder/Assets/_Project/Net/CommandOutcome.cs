#nullable enable

using System;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The end of one player intent, as <see cref="CommandSender"/> reports it: what was meant, under which request id, how it ended and
    /// after how many sends. Views show a message looked up by <see cref="Error"/>'s code, never a failure of their own making.
    /// </summary>
    public sealed class CommandOutcome
    {
        /// <summary>Creates an outcome; <see cref="CommandSender"/> is the only maker.</summary>
        internal CommandOutcome(CommandOutcomeKind kind, PendingCommand command, ApiError? error)
        {
            Kind = kind;
            Intent = command.Intent;
            RequestId = command.RequestId;
            Attempts = command.Attempts;
            Error = error;
        }

        /// <summary>How the intent ended.</summary>
        public CommandOutcomeKind Kind { get; }

        /// <summary>What the player meant.</summary>
        public CommandIntent Intent { get; }

        /// <summary>The request id of the intent, the same on every send of it.</summary>
        public Guid RequestId { get; }

        /// <summary>How many requests were sent for the intent: 0 for a <see cref="CommandOutcomeKind.Superseded"/> one, otherwise 1 up to the policy's maximum.</summary>
        public int Attempts { get; }

        /// <summary>Why a <see cref="CommandOutcomeKind.Failed"/> intent failed: the last error seen; null for the other kinds.</summary>
        public ApiError? Error { get; }

        /// <summary>Whether the server accepted the command.</summary>
        public bool IsSuccess => Kind == CommandOutcomeKind.Succeeded;
    }
}
