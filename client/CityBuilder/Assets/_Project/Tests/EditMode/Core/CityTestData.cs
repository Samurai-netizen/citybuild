#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Tests.EditMode.Net;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// What the city store and prediction tests share: the real content and snapshots of <c>Tests/Fixtures</c> in the engine's terms, and a text
    /// of every stored value of a city, so two cities compare exactly and a failure shows the difference.
    /// </summary>
    internal static class CityTestData
    {
        /// <summary>The real content of the fixtures, through the validating factory.</summary>
        public static GameContent Content()
        {
            ContentResponseDto dto = new ApiJson().Deserialize<ContentResponseDto>(Fixtures.Read("content.json"))!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(dto));
            Assert.That(built.IsValid, Is.True, "The content fixture must be valid.");
            return built.Content!;
        }

        /// <summary>The decoded answer in the fixture file <paramref name="name"/>.</summary>
        public static CitySnapshotDto Dto(string name)
        {
            return new ApiJson().Deserialize<CitySnapshotDto>(Fixtures.Read(name))!;
        }

        /// <summary>The snapshot in the fixture file <paramref name="name"/>.</summary>
        public static CitySnapshot Snapshot(string name)
        {
            ApiResult<CitySnapshot> mapped = SnapshotMapper.ToSnapshot(Dto(name));
            Assert.That(mapped.IsSuccess, Is.True, name + ": " + mapped.Error);
            return mapped.Value!;
        }

        /// <summary>A snapshot of an empty city with the given version and clock, for tests about which snapshot wins.</summary>
        public static CitySnapshot EmptySnapshot(long stateVersion, long clock)
        {
            var city = new CityState(clock, 1, 1, Array.Empty<KeyValuePair<ItemId, long>>(), Array.Empty<Building>(), Array.Empty<ResearchId>(), null);
            return new CitySnapshot(stateVersion, "mvp-1", city);
        }

        /// <summary>
        /// Every stored value of <paramref name="city"/> as text. A building's values are read by reflection, so a field added to
        /// <see cref="Building"/> later is compared too, and a copy that forgets it fails.
        /// </summary>
        public static string Describe(CityState city)
        {
            IEnumerable<string> stock = city.Inventory.Select(item => item.Key + "=" + item.Value);
            string research = city.ActiveResearch == null
                ? "none"
                : city.ActiveResearch.Id + " " + city.ActiveResearch.StartedAt + " to " + city.ActiveResearch.EndsAt;
            return "clock " + city.Clock + ", era " + city.Era + ", next id " + city.NextBuildingId + "\n"
                + "stock " + string.Join(" ", stock) + "\n"
                + "research " + research + ", completed " + string.Join(" ", city.CompletedResearch) + "\n"
                + string.Join("\n", city.Buildings.Select(Describe));
        }

        /// <summary>Every public property of <paramref name="building"/> as text.</summary>
        private static string Describe(Building building)
        {
            IEnumerable<string> values = typeof(Building)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => property.Name + "=" + property.GetValue(building));
            return string.Join(" ", values);
        }
    }
}
