#nullable enable

using System;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// One player intent that has not ended yet, as <see cref="CommandSender.Pending"/> lists it: the UI shows a ghost on a tile, a
    /// pending stepper value or a disabled button from it, and never changes the city itself (<c>09</c> UX rules). Only the sender changes
    /// its members, on the main thread.
    /// </summary>
    public sealed class PendingCommand
    {
        /// <summary>Creates a pending command that has not been sent.</summary>
        internal PendingCommand(Guid requestId, CommandIntent intent)
        {
            RequestId = requestId;
            Intent = intent;
        }

        /// <summary>The intent's request id: made once, sent with every attempt.</summary>
        public Guid RequestId { get; }

        /// <summary>What the player meant, for example the tile and type of a ghost or the target of a stepper.</summary>
        public CommandIntent Intent { get; }

        /// <summary>Where the command is on its way.</summary>
        public CommandPhase Phase { get; internal set; } = CommandPhase.Waiting;

        /// <summary>How many requests have been sent for it so far; 0 until the first one.</summary>
        public int Attempts { get; internal set; }
    }
}
