#nullable enable
using System.Collections.Generic;
using System.Linq;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode
{
    /// <summary>
    /// Proves that the city state and its mutations of the local package run on Unity's runtime the way they run on .NET: the sorted stock,
    /// the building lookup, a rejection that changes nothing, and the labor segment update. The rules themselves are tested in
    /// <c>server/tests/CityBuilder.Simulation.Tests/Rules</c>.
    /// </summary>
    public sealed class CityRulesInUnityTests
    {
        /// <summary>Placing a camp on the starter city pays its coins from the purse and starts the construction under the next id.</summary>
        [Test]
        public void PlaceBuilding_StarterCity_PaysAndConstructs()
        {
            GameContent content = SmallContent();
            CityState city = CityFactory.CreateStarterCity(content);

            MutationResult result = CityRules.PlaceBuilding(city, content, new BuildingTypeId("camp"), 1, 2);

            Assert.That(result.IsOk, Is.True, result.ToString());
            Assert.That(city.Purse, Is.EqualTo(12L));
            Assert.That(city.TryGetBuilding(1, out Building? camp), Is.True);
            // TryGetBuilding returned true, so camp is set.
            Assert.That((camp!.Phase, camp.PhaseEndsAt, city.NextBuildingId), Is.EqualTo((BuildingPhase.Constructing, (long?)6, 2)));
        }

        /// <summary>A placement on a taken tile is rejected with <c>TileOccupied</c> and changes nothing.</summary>
        [Test]
        public void PlaceBuilding_TileTaken_IsRejectedAndChangesNothing()
        {
            GameContent content = SmallContent();
            CityState city = CityFactory.CreateStarterCity(content);
            CityRules.PlaceBuilding(city, content, new BuildingTypeId("hut"), 0, 0);

            MutationResult result = CityRules.PlaceBuilding(city, content, new BuildingTypeId("camp"), 0, 0);

            Assert.That(result.ErrorCode, Is.EqualTo(MutationErrorCode.TileOccupied));
            Assert.That((city.Purse, city.Buildings.Count, city.NextBuildingId), Is.EqualTo((15L, 1, 2)));
        }

        /// <summary>A worker change mid-cycle keeps the labor done and starts a new segment; taking every worker away freezes the cycle.</summary>
        [Test]
        public void AssignWorkers_MidCycle_KeepsProgressThenFreezes()
        {
            GameContent content = SmallContent();
            var hut = new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);

            // A 20-labor cycle with 1 worker from 100; at 110, 10 labor is left.
            var camp = new Building(2, new BuildingTypeId("camp"), 1, 0, 1, 1, BuildingPhase.Producing, 100, 120, 0, 20, 20, 100);
            var city = new CityState(110, 1, 3, 0, new[] { camp, hut }, System.Array.Empty<ResearchId>(), null);

            Assert.That(CityRules.AssignWorkers(city, content, 2, 2).IsOk, Is.True);
            Assert.That((camp.LaborRemaining, camp.SegmentStartedAt, camp.PhaseEndsAt), Is.EqualTo((10L, 110L, (long?)115)));

            Assert.That(CityRules.AssignWorkers(city, content, 2, 0).IsOk, Is.True);
            Assert.That((camp.LaborRemaining, camp.PhaseEndsAt), Is.EqualTo((10L, (long?)null)));
        }

        /// <summary>One era, a good and the currency, a house, a producer with a store and the science building; the numbers are made up for this test.</summary>
        private static GameContent SmallContent()
        {
            var data = new GameContentData
            {
                ContentVersion = "unity-test-1",
                Rules = new GameRulesData
                {
                    GridWidth = 4,
                    GridHeight = 4,
                    BuilderSlots = 2,
                    ResearchSlots = 1,
                    MinCycleSeconds = 2,
                    MaxLaborReductionPermille = 300,
                    GroundTileMillis = 3000,
                    CrossingSpeedPermille = 200,
                    MinWalkSeconds = 1,
                    MaxWalkBonusPermille = 500,
                    TripCarryLimit = 5,
                    MaxRouteStops = 4,
                    MaxStopActions = 2,
                    MaxDeliveryLines = 20,
                },
                Roads = new List<RoadLevelData?> { new RoadLevelData { Level = 1, Name = "Path", TileMillis = 1500, Cost = new Dictionary<string, long?> { ["gold"] = 1 } } },
                StartingCity = new StartingCityData { Era = 1, Purse = 20 },
                Eras = new List<EraData?> { new EraData { Number = 1, Id = "first_era", Name = "First Era" } },
                Items = new List<ItemData?>
                {
                    new ItemData { Id = "wood", Name = "Wood", Era = 1, Currency = false, Tradable = true },
                    new ItemData { Id = "gold", Name = "Gold", Era = 1, Currency = true, Tradable = false },
                },
                Buildings = new List<BuildingData?>
                {
                    new BuildingData
                    {
                        Id = "hut", Name = "Hut", Kind = "house", Era = 1, AvailableAtStart = true,
                        Cost = new Dictionary<string, long?> { ["gold"] = 5 }, BuildSeconds = 3,
                        Levels = new List<BuildingLevelData?> { new BuildingLevelData { Level = 1, WorkersProvided = 3 } },
                    },
                    new BuildingData
                    {
                        Id = "camp", Name = "Camp", Kind = "producer", Era = 1, AvailableAtStart = true,
                        Cost = new Dictionary<string, long?> { ["gold"] = 8 }, BuildSeconds = 6,
                        Levels = new List<BuildingLevelData?>
                        {
                            new BuildingLevelData
                            {
                                Level = 1,
                                MaxWorkers = 2,
                                Recipe = new RecipeData { Outputs = new Dictionary<string, long?> { ["wood"] = 1 }, Labor = 20 },
                                Storage = new Dictionary<string, long?> { ["wood"] = 50 },
                            },
                        },
                    },
                    new BuildingData
                    {
                        Id = "study", Name = "Study", Kind = "science", Era = 1, AvailableAtStart = true, MaxPerCity = 1,
                        Cost = new Dictionary<string, long?> { ["gold"] = 4 }, BuildSeconds = 2,
                        Levels = new List<BuildingLevelData?> { new BuildingLevelData { Level = 1 } },
                    },
                },
                Research = new List<ResearchData?>(),
                AmountPresets = new List<AmountPresetData?> { new AmountPresetData { Id = "all", Name = "All", Percent = 100 } },
            };

            GameContentResult result = GameContentFactory.Create(data);
            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors.Select(error => error.ToString())));

            // IsValid was asserted just above, so Content is set.
            return result.Content!;
        }
    }
}
