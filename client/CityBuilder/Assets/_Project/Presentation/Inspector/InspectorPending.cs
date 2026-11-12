#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Net;

namespace CityBuilder.Client.Presentation.Inspector
{
    /// <summary>
    /// What is still on its way to the server for one building, as far as the inspector shows it (M0-51): the worker target the stepper
    /// sent, the upgrade it asked for and a pause or resume (CR-0008 D23). Read from <see cref="CommandSender.Pending"/> and never stored, so the sender stays the one place
    /// that knows what is pending.
    /// </summary>
    public readonly struct InspectorPending
    {
        /// <summary>Makes the value from its parts.</summary>
        /// <param name="workers">The worker target on its way, or null when none is.</param>
        /// <param name="upgrade">Whether an upgrade is on its way.</param>
        /// <param name="pause">The pause (true) or resume (false) on its way, or null when none is.</param>
        public InspectorPending(int? workers, bool upgrade, bool? pause = null)
        {
            Workers = workers;
            Upgrade = upgrade;
            Pause = pause;
        }

        /// <summary>The worker target on its way, or null when none is.</summary>
        public int? Workers { get; }

        /// <summary>Whether an upgrade of the building is on its way.</summary>
        public bool Upgrade { get; }

        /// <summary>The pause (true) or resume (false) of the building on its way, the newest one; null when none is.</summary>
        public bool? Pause { get; }

        /// <summary>
        /// Reads the pending state of one building from the sender's list. A worker target on its way is listed before the one waiting after
        /// it, so the last target is the newest; commands of other buildings and other kinds are ignored.
        /// </summary>
        /// <param name="buildingId">The building.</param>
        /// <param name="pending">The sender's pending commands.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pending"/> is null.</exception>
        public static InspectorPending From(int buildingId, IReadOnlyList<PendingCommand> pending)
        {
            if (pending == null)
            {
                throw new ArgumentNullException(nameof(pending));
            }

            int? workers = null;
            bool upgrade = false;
            bool? pause = null;
            for (int index = 0; index < pending.Count; index++)
            {
                CommandIntent intent = pending[index].Intent;
                if (intent.BuildingId != buildingId)
                {
                    continue;
                }

                if (intent.Kind == CommandKind.AssignWorkers)
                {
                    workers = intent.Workers;
                }
                else if (intent.Kind == CommandKind.UpgradeBuilding)
                {
                    upgrade = true;
                }
                else if (intent.Kind == CommandKind.SetPaused)
                {
                    pause = intent.Paused;
                }
            }

            return new InspectorPending(workers, upgrade, pause);
        }
    }
}
