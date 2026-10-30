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
        public SnapshotCityDto(long clock, int era, Dictionary<string, long> inventory, Dictionary<string, long> storageCaps, SnapshotWorkersDto workers, SnapshotBuildersDto builders, SnapshotResearchDto research, List<SnapshotBuildingDto> buildings)
        {
            Clock = clock;
            Era = era;
            Inventory = inventory;
            StorageCaps = storageCaps;
            Workers = workers;
            Builders = builders;
            Research = research;
            Buildings = buildings;
        }

        /// <summary>The city clock in seconds; display prediction starts from it.</summary>
        public long Clock { get; }

        /// <summary>The city's era number.</summary>
        public int Era { get; }

        /// <summary>Item id to quantity; an item the city has none of is absent.</summary>
        public Dictionary<string, long> Inventory { get; }

        /// <summary>Item id to its cap now (base plus research), for every capped item.</summary>
        public Dictionary<string, long> StorageCaps { get; }

        /// <summary>Provided, assigned and free workers.</summary>
        public SnapshotWorkersDto Workers { get; }

        /// <summary>Builder slots and how many are busy.</summary>
        public SnapshotBuildersDto Builders { get; }

        /// <summary>The running research and the completed ones.</summary>
        public SnapshotResearchDto Research { get; }

        /// <summary>The buildings, sorted by id.</summary>
        public List<SnapshotBuildingDto> Buildings { get; }
    }
}
