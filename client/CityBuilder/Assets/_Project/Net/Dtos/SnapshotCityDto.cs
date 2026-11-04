#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The city as the server stores it after a request (<c>05</c> Snapshot).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotCityDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotCityDto(long clock, int era, long purse, Dictionary<string, long> totals, SnapshotWorkersDto workers, SnapshotBuildersDto builders, SnapshotResearchDto research, List<SnapshotBuildingDto> buildings, List<SnapshotRoadDto> roads, List<SnapshotTripDto> trips)
        {
            Clock = clock;
            Era = era;
            Purse = purse;
            Totals = totals;
            Workers = workers;
            Builders = builders;
            Research = research;
            Buildings = buildings;
            Roads = roads;
            Trips = trips;
        }

        /// <summary>The city clock in seconds; display prediction starts from it.</summary>
        public long Clock { get; }

        /// <summary>The city's era number.</summary>
        public int Era { get; }

        /// <summary>The coins: the currency, in no building (CR-0008).</summary>
        public long Purse { get; }

        /// <summary>Item id to the city's total, every item of the content (stores, loads, the purse; CR-0008).</summary>
        public Dictionary<string, long> Totals { get; }

        /// <summary>Provided, assigned and free workers.</summary>
        public SnapshotWorkersDto Workers { get; }

        /// <summary>Builder slots and how many are busy.</summary>
        public SnapshotBuildersDto Builders { get; }

        /// <summary>The running research and the completed ones.</summary>
        public SnapshotResearchDto Research { get; }

        /// <summary>The buildings, sorted by id.</summary>
        public List<SnapshotBuildingDto> Buildings { get; }

        /// <summary>The road tiles, sorted by column, then row (CR-0008).</summary>
        public List<SnapshotRoadDto> Roads { get; }

        /// <summary>The manual trips under way, sorted by id (CR-0008).</summary>
        public List<SnapshotTripDto> Trips { get; }
    }
}
