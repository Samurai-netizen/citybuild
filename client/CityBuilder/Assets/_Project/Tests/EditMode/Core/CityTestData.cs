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
            var city = new CityState(clock, 1, 1, 0, Array.Empty<Building>(), Array.Empty<ResearchId>(), null);
            return new CitySnapshot(stateVersion, "mvp-2", city);
        }

        /// <summary>The id of the warehouse <see cref="GoodsStore"/> makes; test cities keep their own ids below it.</summary>
        public const int GoodsStoreId = 99;

        /// <summary>
        /// A finished warehouse on the far corner tile (7, 7) holding <paramref name="logs"/> and <paramref name="planks"/>, or nothing when both
        /// are 0: since CR-0008 goods live in building stores, so a test city with goods keeps them here and its totals stay what the test says.
        /// </summary>
        public static IEnumerable<Building> GoodsStore(long logs, long planks)
        {
            if (logs == 0 && planks == 0)
            {
                yield break;
            }

            var store = new List<KeyValuePair<ItemId, long>>();
            if (logs != 0)
            {
                store.Add(new KeyValuePair<ItemId, long>(new ItemId("logs"), logs));
            }

            if (planks != 0)
            {
                store.Add(new KeyValuePair<ItemId, long>(new ItemId("planks"), planks));
            }

            yield return new Building(GoodsStoreId, new BuildingTypeId("warehouse"), 7, 7, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, store);
        }

        /// <summary>
        /// Every stored value of <paramref name="city"/> as text. A building's values are read by reflection, so a field added to
        /// <see cref="Building"/> later is compared too, and a copy that forgets it fails; its store and needs are listed by good.
        /// </summary>
        public static string Describe(CityState city)
        {
            string research = city.ActiveResearch == null
                ? "none"
                : city.ActiveResearch.Id + " " + city.ActiveResearch.StartedAt + " to " + city.ActiveResearch.EndsAt + " needs "
                    + Amounts(city.ActiveResearch.Needs);
            IEnumerable<string> trips = city.Trips.Select(
                trip => "trip " + trip.Id + " " + trip.SourceId + "->" + trip.TargetId + " " + trip.Quantity + " " + trip.Item + " " + trip.DepartedAt
                    + " to " + trip.ArrivesAt);
            return "clock " + city.Clock + ", era " + city.Era + ", next id " + city.NextBuildingId + ", next trip " + city.NextTripId + "\n"
                + "purse " + city.Purse + "\n"
                + "research " + research + ", completed " + string.Join(" ", city.CompletedResearch) + "\n"
                + string.Join("\n", city.Buildings.Select(Describe)) + "\n"
                + string.Join("\n", trips);
        }

        /// <summary>Every public property of <paramref name="building"/> as text.</summary>
        private static string Describe(Building building)
        {
            IEnumerable<string> values = typeof(Building)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => property.Name + "=" + Value(property.GetValue(building)));
            return string.Join(" ", values);
        }

        /// <summary>A property's value as text; a store or needs dictionary lists its goods.</summary>
        private static string Value(object? value)
        {
            return value is IReadOnlyDictionary<ItemId, long> amounts ? Amounts(amounts) : value?.ToString() ?? "null";
        }

        /// <summary>Goods and quantities in ordinal id order, such as <c>{logs=4 planks=2}</c>.</summary>
        private static string Amounts(IReadOnlyDictionary<ItemId, long> amounts)
        {
            return "{" + string.Join(" ", amounts.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)) + "}";
        }
    }
}
