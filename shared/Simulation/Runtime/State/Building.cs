#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// A building placed in a city (<c>core-instructions/04-simulation-model.md</c>, sections 3, 4 and 16): its phase, workers and labor, its
    /// store and the goods its project still needs. Times are city-clock seconds and labor is in worker-seconds. Only the engine changes a
    /// building, through <see cref="Rules.CityRules"/> and <see cref="Settlement.Simulator"/>; everything else reads it. The fields a phase
    /// doesn't use are 0, and <see cref="PhaseEndsAt"/> is null (<see cref="BuildingPhase"/>). A <b>construction site</b> is
    /// <see cref="BuildingPhase.Constructing"/> without a timer and with needs; an <b>upgrade waiting for goods</b> is an
    /// <see cref="BuildingPhase.Idle"/> or <see cref="BuildingPhase.Producing"/> building with needs.
    /// </summary>
    public sealed class Building
    {
        /// <summary>The goods in the building's store, only quantities above 0, sorted by item id.</summary>
        private readonly ItemTotals _store = new ItemTotals();

        /// <summary>The goods the building's project still needs, only quantities above 0, sorted by item id; empty when no project waits.</summary>
        private readonly ItemTotals _needs = new ItemTotals();

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
        /// <param name="store">
        /// The goods in the store, each at most once, none below 0 (0 is left out); null for none. Whether the level's store holds each good is
        /// the caller's business, like the level.
        /// </param>
        /// <param name="needs">
        /// The goods the project still needs, each at most once, none below 0 (0 is left out); null for none. Needs are allowed on a construction
        /// site (constructing with no start and no end) and on an idle or producing building (its upgrade waits for them), nowhere else.
        /// </param>
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
            long segmentStartedAt,
            IEnumerable<KeyValuePair<ItemId, long>>? store = null,
            IEnumerable<KeyValuePair<ItemId, long>>? needs = null)
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
            Store = _store;
            Needs = _needs;

            string? problem = Fill(_store, store, "store") ?? Fill(_needs, needs, "needs") ?? FindProblem();
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

        /// <summary>
        /// The goods in the building's store (<c>04</c> section 16), by item id; a good it holds none of is absent. The level's
        /// <see cref="BuildingLevelDefinition.Storage"/> says which goods the store holds and their limits.
        /// </summary>
        public IReadOnlyDictionary<ItemId, long> Store { get; }

        /// <summary>
        /// The goods the building's project still needs (<c>04</c> section 16), by item id: a construction site's or a waiting upgrade's. Empty
        /// when no project waits.
        /// </summary>
        public IReadOnlyDictionary<ItemId, long> Needs { get; }

        /// <summary>Whether the building is a construction site: placed, waiting for goods, its build timer not running yet.</summary>
        public bool IsConstructionSite => Phase == BuildingPhase.Constructing && _needs.Count > 0;

        /// <summary>Whether an upgrade waits for goods: the building keeps working, and its builder slot is taken (<c>04</c> section 8).</summary>
        public bool HasUpgradeWaiting => (Phase == BuildingPhase.Idle || Phase == BuildingPhase.Producing) && _needs.Count > 0;

        /// <summary>How much of a good the store holds.</summary>
        /// <param name="item">The good.</param>
        /// <returns>The quantity, 0 when it holds none.</returns>
        public long GetStock(ItemId item) => _store.Get(item);

        /// <summary>How much of a good the project still needs.</summary>
        /// <param name="item">The good.</param>
        /// <returns>The quantity, 0 when it needs none.</returns>
        public long GetNeed(ItemId item) => _needs.Get(item);

        /// <summary>Starts a new building: level 1, no workers, constructing from <paramref name="startedAt"/> to <paramref name="endsAt"/>.</summary>
        internal static Building StartConstruction(int id, BuildingTypeId typeId, int x, int y, long startedAt, long endsAt) =>
            new Building(id, typeId, x, y, 1, 0, BuildingPhase.Constructing, startedAt, endsAt, 0, 0, 0, 0);

        /// <summary>
        /// Starts a construction site: level 1, no workers, constructing without a timer, waiting for <paramref name="needs"/> (at least one good
        /// above 0). <see cref="StartConstructionTimer"/> starts it when they are in.
        /// </summary>
        internal static Building StartConstructionSite(int id, BuildingTypeId typeId, int x, int y, IReadOnlyList<ItemQuantity> needs)
        {
            var building = new Building(id, typeId, x, y, 1, 0, BuildingPhase.Constructing, 0, null, 0, 0, 0, 0, null, ToPairs(needs));
            if (!building.IsConstructionSite)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Building {id}: a construction site needs at least one good."));
            }

            return building;
        }

        /// <summary>A construction site's needs are in: the build timer runs from <paramref name="startedAt"/> to <paramref name="endsAt"/>.</summary>
        internal void StartConstructionTimer(long startedAt, long endsAt)
        {
            RequirePhase(BuildingPhase.Constructing);
            if (_needs.Count > 0 || PhaseEndsAt.HasValue || endsAt < startedAt)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Building {Id}: only a site whose goods are in starts its build timer."));
            }

            PhaseStartedAt = startedAt;
            PhaseEndsAt = endsAt;
        }

        /// <summary>An upgrade was ordered and waits for <paramref name="needs"/> (at least one good above 0); the building keeps working meanwhile.</summary>
        internal void WaitForUpgrade(IReadOnlyList<ItemQuantity> needs)
        {
            if ((Phase != BuildingPhase.Idle && Phase != BuildingPhase.Producing) || _needs.Count > 0)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Building {Id}: only an idle or producing building without a project waits for an upgrade."));
            }

            AddAll(_needs, needs);
            if (_needs.Count == 0)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Building {Id}: a waiting upgrade needs at least one good."));
            }
        }

        /// <summary>
        /// Hands up to <paramref name="quantity"/> of a good to the project: at most what it still needs. Returns what it took; the caller keeps
        /// the rest (<c>04</c> section 16, deliveries).
        /// </summary>
        internal long FillNeed(ItemId item, long quantity)
        {
            long taken = Math.Min(Math.Max(quantity, 0), _needs.Get(item));
            _needs.Set(item, checked(_needs.Get(item) - taken));
            return taken;
        }

        /// <summary>Puts goods into the store. The caller checks the room; a refund and a returned load may go above the limit (<c>04</c> section 16).</summary>
        internal void AddToStore(ItemId item, long quantity) => _store.Set(item, checked(_store.Get(item) + quantity));

        /// <summary>Puts every amount of a list into the store (a refund, an output), as <see cref="AddToStore(ItemId, long)"/>.</summary>
        internal void AddToStore(IReadOnlyList<ItemQuantity> amounts)
        {
            for (int index = 0; index < amounts.Count; index++)
            {
                AddToStore(amounts[index].Item, amounts[index].Quantity);
            }
        }

        /// <summary>Takes goods out of the store. The caller checks the stock; taking more than there is throws (the engine's own bug).</summary>
        internal void TakeFromStore(ItemId item, long quantity)
        {
            long left = checked(_store.Get(item) - quantity);
            if (left < 0)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Building {Id}: took {quantity} {item} with only {_store.Get(item)} in its store."));
            }

            _store.Set(item, left);
        }

        /// <summary>Takes every amount of a list out of the store (a cycle's inputs), as <see cref="TakeFromStore(ItemId, long)"/>.</summary>
        internal void TakeFromStore(IReadOnlyList<ItemQuantity> amounts)
        {
            for (int index = 0; index < amounts.Count; index++)
            {
                TakeFromStore(amounts[index].Item, amounts[index].Quantity);
            }
        }

        /// <summary>
        /// Starts the upgrade to the next level. A running cycle ends without output: the caller refunds its inputs. The workers stay assigned.
        /// </summary>
        internal void StartUpgrade(long startedAt, long endsAt)
        {
            if (_needs.Count > 0)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Building {Id}: an upgrade starts only when its goods are in."));
            }

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

        /// <summary>Throws when the building is not in the phase a transition needs: the engine's own bug, never a player's mistake.</summary>
        private void RequirePhase(BuildingPhase phase)
        {
            if (Phase != phase)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Building {Id} is {Phase}, not {phase}."));
            }
        }

        /// <summary>Sets every field a phase uses back to 0 or null, the canonical form of an idle building (decision 030).</summary>
        private void ClearPhaseFields()
        {
            PhaseStartedAt = 0;
            PhaseEndsAt = null;
            TargetLevel = 0;
            CycleLabor = 0;
            LaborRemaining = 0;
            SegmentStartedAt = 0;
        }

        /// <summary>
        /// Why the fields can't form a building, as the end of the restoring constructor's error message, or null when they can: each phase
        /// uses exactly its own fields, and the others are 0 or null.
        /// </summary>
        private string? FindProblem()
        {
            if (Id < 1 || X < 0 || Y < 0 || Level < 1 || AssignedWorkers < 0 || PhaseStartedAt < 0)
            {
                return "the id and level must be at least 1, and the tile, workers and start must not be negative.";
            }

            switch (Phase)
            {
                case BuildingPhase.Constructing:
                    bool timerOrSite = _needs.Count == 0
                        ? PhaseEndsAt >= PhaseStartedAt
                        : PhaseStartedAt == 0 && PhaseEndsAt == null;
                    return timerOrSite && AssignedWorkers == 0 && TargetLevel == 0 && HasNoCycle()
                        ? null
                        : "a constructing building has no workers, no target level or cycle, and either an end not before its start, or (a "
                          + "construction site) needs and no start or end.";
                case BuildingPhase.Upgrading:
                    return PhaseEndsAt >= PhaseStartedAt && TargetLevel == (long)Level + 1 && HasNoCycle() && _needs.Count == 0
                        ? null
                        : "an upgrading building has an end not before its start, the target level one above its level, no cycle and no needs.";
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

        /// <summary>
        /// Fills one of the building's quantity lists from the restoring constructor's values: each good at most once and none below 0 (0 is
        /// left out). Returns why the values can't be used, or null.
        /// </summary>
        private static string? Fill(ItemTotals totals, IEnumerable<KeyValuePair<ItemId, long>>? values, string name)
        {
            if (values is null)
            {
                return null;
            }

            var seen = new HashSet<ItemId>();
            foreach (KeyValuePair<ItemId, long> value in values)
            {
                if (value.Value < 0 || !seen.Add(value.Key))
                {
                    return FormattableString.Invariant($"its {name} lists {value.Key} twice or below 0.");
                }

                totals.Set(value.Key, value.Value);
            }

            return null;
        }

        /// <summary>Adds every amount of a list to a quantity list, <c>checked</c>.</summary>
        private static void AddAll(ItemTotals totals, IReadOnlyList<ItemQuantity> amounts)
        {
            for (int index = 0; index < amounts.Count; index++)
            {
                totals.Set(amounts[index].Item, checked(totals.Get(amounts[index].Item) + amounts[index].Quantity));
            }
        }

        /// <summary>A list of amounts as pairs, for the restoring constructor.</summary>
        private static List<KeyValuePair<ItemId, long>> ToPairs(IReadOnlyList<ItemQuantity> amounts)
        {
            var pairs = new List<KeyValuePair<ItemId, long>>(amounts.Count);
            for (int index = 0; index < amounts.Count; index++)
            {
                pairs.Add(new KeyValuePair<ItemId, long>(amounts[index].Item, amounts[index].Quantity));
            }

            return pairs;
        }

        /// <summary>Whether no cycle field is set: required in every phase except producing.</summary>
        private bool HasNoCycle() => CycleLabor == 0 && LaborRemaining == 0 && SegmentStartedAt == 0;

        /// <summary>
        /// Whether a producing building's end is exactly <c>SegmentStartedAt + CeilDiv(LaborRemaining, AssignedWorkers)</c> (<c>04</c> section 3),
        /// the only value <see cref="StartCycle"/> and <see cref="AssignWorkers"/> give it; a restored building with another end would finish its
        /// labor early or late. A frozen cycle has no end. The difference is compared, so huge values can't overflow, and the remaining labor is
        /// checked first, so <see cref="IntegerMath.CeilDiv"/> gets no negative dividend.
        /// </summary>
        private bool EndsWhenTheLaborIsDone() =>
            PhaseEndsAt == null
            || (LaborRemaining >= 0 && PhaseEndsAt >= SegmentStartedAt
                && PhaseEndsAt.Value - SegmentStartedAt == IntegerMath.CeilDiv(LaborRemaining, AssignedWorkers));
    }
}
