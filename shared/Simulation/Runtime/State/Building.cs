#nullable enable
using System;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// A building placed in a city (<c>core-instructions/04-simulation-model.md</c>, sections 3 and 4). Times are city-clock seconds and labor
    /// is in worker-seconds. Only the engine changes a building, through <see cref="Rules.CityRules"/> and <see cref="Settlement.Simulator"/>;
    /// everything else reads it. The fields a phase doesn't use are 0, and <see cref="PhaseEndsAt"/> is null (<see cref="BuildingPhase"/>).
    /// </summary>
    public sealed class Building
    {
        /// <summary>
        /// Restores a building with every field, for example from the database. The values must form a valid building: the checks below are
        /// those that need no content (that the type exists and the level is within its max is the caller's business).
        /// </summary>
        /// <param name="id">The building's id in its city, at least 1.</param>
        /// <param name="typeId">The building type.</param>
        /// <param name="x">The tile column, at least 0.</param>
        /// <param name="y">The tile row, at least 0.</param>
        /// <param name="level">The level, at least 1.</param>
        /// <param name="assignedWorkers">The workers assigned, at least 0, and 0 while constructing.</param>
        /// <param name="phase">What the building is doing.</param>
        /// <param name="phaseStartedAt">When the phase (or, while producing, the cycle) started; 0 when idle.</param>
        /// <param name="phaseEndsAt">
        /// When the phase ends: set while constructing or upgrading and while producing with workers; null when idle or frozen (producing with
        /// 0 workers); never before the start. While producing it is exactly <c>segmentStartedAt + CeilDiv(laborRemaining, assignedWorkers)</c>.
        /// </param>
        /// <param name="targetLevel">The level an upgrade reaches, <paramref name="level"/> + 1, while upgrading; otherwise 0.</param>
        /// <param name="cycleLabor">The running cycle's labor, at least 1, while producing; otherwise 0.</param>
        /// <param name="laborRemaining">The labor left at <paramref name="segmentStartedAt"/>, 0 to <paramref name="cycleLabor"/>, while producing; otherwise 0.</param>
        /// <param name="segmentStartedAt">When the worker count last changed in the running cycle, while producing; otherwise 0.</param>
        /// <exception cref="ArgumentException">The values don't form a valid building; the message says which rule they break.</exception>
        public Building(
            int id,
            BuildingTypeId typeId,
            int x,
            int y,
            int level,
            int assignedWorkers,
            BuildingPhase phase,
            long phaseStartedAt,
            long? phaseEndsAt,
            int targetLevel,
            long cycleLabor,
            long laborRemaining,
            long segmentStartedAt)
        {
            Id = id;
            TypeId = typeId;
            X = x;
            Y = y;
            Level = level;
            AssignedWorkers = assignedWorkers;
            Phase = phase;
            PhaseStartedAt = phaseStartedAt;
            PhaseEndsAt = phaseEndsAt;
            TargetLevel = targetLevel;
            CycleLabor = cycleLabor;
            LaborRemaining = laborRemaining;
            SegmentStartedAt = segmentStartedAt;

            string? problem = FindProblem();
            if (problem != null)
            {
                throw new ArgumentException(FormattableString.Invariant($"Building {Id} is not valid: {problem}"));
            }
        }

        /// <summary>The building's id in its city: unique, never reused, given in placement order from 1.</summary>
        public int Id { get; }

        /// <summary>The building type.</summary>
        public BuildingTypeId TypeId { get; }

        /// <summary>The tile column, from 0 to the grid width − 1. Every building takes one tile in M0.</summary>
        public int X { get; }

        /// <summary>The tile row, from 0 to the grid height − 1.</summary>
        public int Y { get; }

        /// <summary>The current level, from 1 to the type's max level. It rises when an upgrade completes.</summary>
        public int Level { get; private set; }

        /// <summary>
        /// The workers assigned by the player (or by auto-staffing when construction completes). They stay assigned during an upgrade; houses
        /// never have any.
        /// </summary>
        public int AssignedWorkers { get; private set; }

        /// <summary>What the building is doing.</summary>
        public BuildingPhase Phase { get; private set; }

        /// <summary>When the current phase started, or while producing, when the running cycle started; 0 when idle.</summary>
        public long PhaseStartedAt { get; private set; }

        /// <summary>When the current phase ends: the construction, upgrade or cycle completes at this second. Null when idle or frozen.</summary>
        public long? PhaseEndsAt { get; private set; }

        /// <summary>The level an upgrade reaches (the level plus one) while upgrading; otherwise 0.</summary>
        public int TargetLevel { get; private set; }

        /// <summary>The running cycle's labor in worker-seconds, fixed when the cycle starts (research reductions included); 0 unless producing.</summary>
        public long CycleLabor { get; private set; }

        /// <summary>
        /// The running cycle's labor still to do at <see cref="SegmentStartedAt"/>, in worker-seconds; 0 unless producing. The labor still to do
        /// at clock <c>t</c> is <c>LaborRemaining − (t − SegmentStartedAt) × AssignedWorkers</c>.
        /// </summary>
        public long LaborRemaining { get; private set; }

        /// <summary>When the running cycle's current labor segment (a stretch with one worker count) started; 0 unless producing.</summary>
        public long SegmentStartedAt { get; private set; }

        /// <summary>Starts a new building: level 1, no workers, constructing from <paramref name="startedAt"/> to <paramref name="endsAt"/>.</summary>
        internal static Building StartConstruction(int id, BuildingTypeId typeId, int x, int y, long startedAt, long endsAt) =>
            new Building(id, typeId, x, y, 1, 0, BuildingPhase.Constructing, startedAt, endsAt, 0, 0, 0, 0);

        /// <summary>
        /// Starts the upgrade to the next level. A running cycle ends without output: the caller refunds its inputs. The workers stay assigned.
        /// </summary>
        internal void StartUpgrade(long startedAt, long endsAt)
        {
            int targetLevel = checked(Level + 1);
            ClearPhaseFields();
            Phase = BuildingPhase.Upgrading;
            PhaseStartedAt = startedAt;
            PhaseEndsAt = endsAt;
            TargetLevel = targetLevel;
        }

        /// <summary>
        /// Sets the worker count at clock <paramref name="now"/>. A running cycle keeps its progress: the labor done since
        /// <see cref="SegmentStartedAt"/> with the old count is taken off, a new segment starts, and the cycle ends after
        /// <c>CeilDiv(LaborRemaining, workers)</c> seconds, or never (frozen) with 0 workers (<c>04</c> section 3).
        /// </summary>
        internal void AssignWorkers(int workers, long now)
        {
            if (Phase == BuildingPhase.Producing)
            {
                if (now < SegmentStartedAt)
                {
                    throw new InvalidOperationException(FormattableString.Invariant($"Building {Id}: the clock is before the start of the labor segment."));
                }

                long laborDone = checked((now - SegmentStartedAt) * AssignedWorkers);
                LaborRemaining = Math.Max(0, checked(LaborRemaining - laborDone));
                SegmentStartedAt = now;
                PhaseEndsAt = workers == 0 ? (long?)null : checked(now + IntegerMath.CeilDiv(LaborRemaining, workers));
            }

            AssignedWorkers = workers;
        }

        /// <summary>
        /// Starts a production cycle at <paramref name="startedAt"/> with the assigned workers (at least 1): <paramref name="labor"/> worker-seconds
        /// to do, ending after <c>CeilDiv(labor, workers)</c> seconds (<c>04</c> section 6). The caller takes the inputs and checks the start rule.
        /// </summary>
        internal void StartCycle(long startedAt, long labor)
        {
            RequirePhase(BuildingPhase.Idle);
            if (AssignedWorkers < 1 || labor < 1)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Building {Id}: a cycle needs at least 1 worker and 1 labor."));
            }

            Phase = BuildingPhase.Producing;
            PhaseStartedAt = startedAt;
            CycleLabor = labor;
            LaborRemaining = labor;
            SegmentStartedAt = startedAt;
            PhaseEndsAt = checked(startedAt + IntegerMath.CeilDiv(labor, AssignedWorkers));
        }

        /// <summary>Ends construction: the building is idle with <paramref name="workers"/> assigned (auto-staffing, <c>04</c> section 3).</summary>
        internal void CompleteConstruction(int workers)
        {
            RequirePhase(BuildingPhase.Constructing);
            if (workers < 0)
            {
                // Free workers below 0 break invariant 9: the city was restored with more workers assigned than its houses provide.
                throw new InvalidOperationException(FormattableString.Invariant($"Building {Id}: auto-staffing found {workers} free workers."));
            }

            ClearPhaseFields();
            Phase = BuildingPhase.Idle;
            AssignedWorkers = workers;
        }

        /// <summary>Ends the upgrade: the building reaches its target level and is idle. The workers stay assigned.</summary>
        internal void CompleteUpgrade()
        {
            RequirePhase(BuildingPhase.Upgrading);
            int targetLevel = TargetLevel;
            ClearPhaseFields();
            Phase = BuildingPhase.Idle;
            Level = targetLevel;
        }

        /// <summary>Ends the running cycle: the building is idle. The caller moves the outputs from reserved to stock.</summary>
        internal void CompleteCycle()
        {
            RequirePhase(BuildingPhase.Producing);
            ClearPhaseFields();
            Phase = BuildingPhase.Idle;
        }

        private void RequirePhase(BuildingPhase phase)
        {
            if (Phase != phase)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Building {Id} is {Phase}, not {phase}."));
            }
        }

        private void ClearPhaseFields()
        {
            PhaseStartedAt = 0;
            PhaseEndsAt = null;
            TargetLevel = 0;
            CycleLabor = 0;
            LaborRemaining = 0;
            SegmentStartedAt = 0;
        }

        private string? FindProblem()
        {
            if (Id < 1 || X < 0 || Y < 0 || Level < 1 || AssignedWorkers < 0 || PhaseStartedAt < 0)
            {
                return "the id and level must be at least 1, and the tile, workers and start must not be negative.";
            }

            switch (Phase)
            {
                case BuildingPhase.Constructing:
                    return PhaseEndsAt >= PhaseStartedAt && AssignedWorkers == 0 && TargetLevel == 0 && HasNoCycle()
                        ? null
                        : "a constructing building has an end not before its start, no workers, and no target level or cycle.";
                case BuildingPhase.Upgrading:
                    return PhaseEndsAt >= PhaseStartedAt && TargetLevel == (long)Level + 1 && HasNoCycle()
                        ? null
                        : "an upgrading building has an end not before its start, the target level one above its level, and no cycle.";
                case BuildingPhase.Idle:
                    return PhaseStartedAt == 0 && PhaseEndsAt == null && TargetLevel == 0 && HasNoCycle()
                        ? null
                        : "an idle building has no start, end, target level or cycle.";
                case BuildingPhase.Producing:
                    return TargetLevel == 0 && CycleLabor >= 1 && LaborRemaining >= 0 && LaborRemaining <= CycleLabor
                        && SegmentStartedAt >= PhaseStartedAt && (PhaseEndsAt == null) == (AssignedWorkers == 0) && EndsWhenTheLaborIsDone()
                        ? null
                        : "a producing building has labor of at least 1, the remaining labor within it, a segment not before the cycle's start, "
                          + "an end exactly when it has workers (no end means frozen), the end its labor and workers give, and no target level.";
                default:
                    return "the phase is not one of the defined phases.";
            }
        }

        private bool HasNoCycle() => CycleLabor == 0 && LaborRemaining == 0 && SegmentStartedAt == 0;

        // The end of a running cycle is SegmentStartedAt + CeilDiv(LaborRemaining, AssignedWorkers) (04 section 3), the only value StartCycle and
        // AssignWorkers give it; a restored building with another end would finish its labor early or late. Frozen cycles have no end. The
        // difference is compared, so huge values can't overflow; the remaining labor is checked first, so CeilDiv gets no negative dividend.
        private bool EndsWhenTheLaborIsDone() =>
            PhaseEndsAt == null
            || (LaborRemaining >= 0 && PhaseEndsAt >= SegmentStartedAt
                && PhaseEndsAt.Value - SegmentStartedAt == IntegerMath.CeilDiv(LaborRemaining, AssignedWorkers));
    }
}
