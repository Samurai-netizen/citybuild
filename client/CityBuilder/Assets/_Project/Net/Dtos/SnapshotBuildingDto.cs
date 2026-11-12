#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One building of the city (<c>05</c> Snapshot, <c>04</c> sections 3 to 6). All times are city-clock seconds.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotBuildingDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotBuildingDto(int id, string typeId, int x, int y, int level, int assignedWorkers, int maxWorkers, BuildingPhaseDto phase, long phaseStartedAt, long? phaseEndsAt, long cycleLabor, long laborRemaining, long segmentStartedAt, string? stallReason, Dictionary<string, long> store, Dictionary<string, long> storeLimits, Dictionary<string, long> needs, string? project, bool paused = false)
        {
            Id = id;
            TypeId = typeId;
            X = x;
            Y = y;
            Level = level;
            AssignedWorkers = assignedWorkers;
            MaxWorkers = maxWorkers;
            Phase = phase;
            PhaseStartedAt = phaseStartedAt;
            PhaseEndsAt = phaseEndsAt;
            CycleLabor = cycleLabor;
            LaborRemaining = laborRemaining;
            SegmentStartedAt = segmentStartedAt;
            StallReason = stallReason;
            Store = store;
            StoreLimits = storeLimits;
            Needs = needs;
            Project = project;
            Paused = paused;
        }

        /// <summary>The building's id, counted from 1 in this city.</summary>
        public int Id { get; }

        /// <summary>The content id of its type.</summary>
        public string TypeId { get; }

        /// <summary>Tile column.</summary>
        public int X { get; }

        /// <summary>Tile row.</summary>
        public int Y { get; }

        /// <summary>Current level, from 1.</summary>
        public int Level { get; }

        /// <summary>Workers assigned now.</summary>
        public int AssignedWorkers { get; }

        /// <summary>Most workers its current level takes, also while it is being built or upgraded (0 for houses).</summary>
        public int MaxWorkers { get; }

        /// <summary>What the building is doing.</summary>
        public BuildingPhaseDto Phase { get; }

        /// <summary>When the phase began; 0 when the phase has no timer.</summary>
        public long PhaseStartedAt { get; }

        /// <summary>When the phase ends, or <c>null</c> when idle, for a frozen production cycle and for a construction site.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public long? PhaseEndsAt { get; }

        /// <summary>Worker-seconds one production cycle needs; 0 when none is running.</summary>
        public long CycleLabor { get; }

        /// <summary>Worker-seconds of the running cycle still to do when the current segment began.</summary>
        public long LaborRemaining { get; }

        /// <summary>When the current constant-workers segment of the cycle began (<c>04</c> section 3).</summary>
        public long SegmentStartedAt { get; }

        /// <summary>Why the cycle is not running: <c>NoRecipe</c>, <c>NoWorkers</c>, <c>Paused</c>, <c>MissingInput:&lt;item&gt;</c> or <c>StorageFull:&lt;item&gt;</c>; <c>null</c> when it is not stalled.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? StallReason { get; }

        /// <summary>Item id to the quantity in its store; only goods it holds (CR-0008).</summary>
        public Dictionary<string, long> Store { get; }

        /// <summary>Item id to the effective limit of every good its level's store holds; empty without a store (CR-0008).</summary>
        public Dictionary<string, long> StoreLimits { get; }

        /// <summary>Item id to what its waiting project still needs; empty without one (CR-0008).</summary>
        public Dictionary<string, long> Needs { get; }

        /// <summary>The project waiting for goods: <c>Construction</c>, <c>Upgrade</c> or <c>null</c> (CR-0008).</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? Project { get; }

        /// <summary>Whether the player paused the building (CR-0008 amendment 2, D23); false for every building that can't be paused.</summary>
        public bool Paused { get; }
    }
}
