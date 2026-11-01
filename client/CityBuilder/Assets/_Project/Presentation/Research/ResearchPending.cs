#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Net;

namespace CityBuilder.Client.Presentation.Research
{
    /// <summary>
    /// Reads from the command sender which research start is still on its way to the server (M0-52), so the screen shows it as pending and
    /// switches the Finance buttons off. Never stored: the sender stays the one place that knows what is pending.
    /// </summary>
    public static class ResearchPending
    {
        /// <summary>
        /// The id of the newest <c>StartResearch</c> in the sender's list, or null when none is on its way. Commands of other kinds are ignored.
        /// </summary>
        /// <param name="pending">The sender's pending commands, oldest first.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pending"/> is null.</exception>
        public static string? From(IReadOnlyList<PendingCommand> pending)
        {
            if (pending == null)
            {
                throw new ArgumentNullException(nameof(pending));
            }

            string? id = null;
            for (int index = 0; index < pending.Count; index++)
            {
                CommandIntent intent = pending[index].Intent;
                if (intent.Kind == CommandKind.StartResearch)
                {
                    id = intent.ContentId;
                }
            }

            return id;
        }
    }
}
