#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// One authoritative server snapshot in the engine's terms (<c>core-instructions/05-api-contract.md</c>, "Snapshot"): what
    /// <see cref="ClientCityStore"/> holds. It is immutable because its <see cref="CityState"/> never leaves this object: a reader gets a
    /// <see cref="CopyCity">copy</see>, so nothing can run the engine on the server's answer by accident. The network layer builds it from the
    /// DTO (Core cannot name the DTOs: <c>03</c>).
    /// </summary>
    public sealed class CitySnapshot
    {
        /// <summary>The city as the server sent it; only copies leave this object.</summary>
        private readonly CityState _city;

        /// <summary>Creates a snapshot.</summary>
        /// <param name="stateVersion">The city's stored version after the request.</param>
        /// <param name="contentVersion">The version of the content the server settled the city with.</param>
        /// <param name="city">The city at the snapshot's clock. The snapshot takes it over: the caller must not keep using it.</param>
        /// <exception cref="ArgumentNullException"><paramref name="contentVersion"/> or <paramref name="city"/> is null.</exception>
        public CitySnapshot(long stateVersion, string contentVersion, CityState city)
        {
            StateVersion = stateVersion;
            ContentVersion = contentVersion ?? throw new ArgumentNullException(nameof(contentVersion));
            _city = city ?? throw new ArgumentNullException(nameof(city));
        }

        /// <summary>The city's stored version after the request; it rises only when a request changed the city.</summary>
        public long StateVersion { get; }

        /// <summary>The version of the content the server settled the city with.</summary>
        public string ContentVersion { get; }

        /// <summary>The city clock of the snapshot, in seconds.</summary>
        public long Clock => _city.Clock;

        /// <summary>
        /// Whether this snapshot is newer than <paramref name="other"/>: a higher version, or the same version with a later clock. The same
        /// version at the same clock is the same city, for example a replayed command's stored answer, so it is not newer.
        /// </summary>
        /// <param name="other">The snapshot to compare with.</param>
        /// <returns>True when this snapshot should replace <paramref name="other"/>.</returns>
        public bool IsNewerThan(CitySnapshot other)
        {
            return StateVersion != other.StateVersion ? StateVersion > other.StateVersion : Clock > other.Clock;
        }

        /// <summary>A new city with every value of this snapshot's, which the caller may read or advance freely.</summary>
        /// <returns>The copy; it shares nothing that can change with the snapshot.</returns>
        public CityState CopyCity()
        {
            var inventory = new List<KeyValuePair<ItemId, long>>(_city.Inventory.Count);
            foreach (KeyValuePair<ItemId, long> stock in _city.Inventory)
            {
                inventory.Add(stock);
            }

            var buildings = new List<Building>(_city.Buildings.Count);
            foreach (Building building in _city.Buildings)
            {
                buildings.Add(new Building(
                    building.Id,
                    building.TypeId,
                    building.X,
                    building.Y,
                    building.Level,
                    building.AssignedWorkers,
                    building.Phase,
                    building.PhaseStartedAt,
                    building.PhaseEndsAt,
                    building.TargetLevel,
                    building.CycleLabor,
                    building.LaborRemaining,
                    building.SegmentStartedAt));
            }

            return new CityState(_city.Clock, _city.Era, _city.NextBuildingId, inventory, buildings, _city.CompletedResearch, _city.ActiveResearch);
        }
    }
}
