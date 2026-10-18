#nullable enable
using System.Collections.Generic;
using System.Linq;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode
{
    /// <summary>
    /// Proves that <c>Simulator.Advance</c> and the stall reasons of the local package run on Unity's runtime the way they run on .NET: timers,
    /// auto-staffing, cycles, the sorted report and the API text of a stall reason. The rules themselves are tested in
    /// <c>server/tests/CityBuilder.Simulation.Tests/Settlement</c>.
    /// </summary>
    public sealed class SimulatorInUnityTests
    {
        [Test]
        public void Advance_PlacedHutAndCamp_CompleteStaffAndProduce()
        {
            GameContent content = SmallContent();
            CityState city = CityFactory.CreateStarterCity(content);
            Assert.That(CityRules.PlaceBuilding(city, content, new BuildingTypeId("hut"), 0, 0).IsOk, Is.True);
            Assert.That(CityRules.PlaceBuilding(city, content, new BuildingTypeId("camp"), 1, 0).IsOk, Is.True);

            // The hut is done at 3 (3 workers), the camp at 6: it gets 2 workers and starts 10-second cycles (20 labor).
            SettlementReport first = Simulator.Advance(city, content, 6);
            SettlementReport second = Simulator.Advance(city, content, 25);

            Assert.That(first.CompletedBuildings, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(city.TryGetBuilding(2, out Building? camp), Is.True);
            // TryGetBuilding returned true, so camp is set.
            Assert.That((camp!.AssignedWorkers, camp.Phase, camp.PhaseStartedAt, camp.PhaseEndsAt), Is.EqualTo((2, BuildingPhase.Producing, 26L, (long?)36)));
            Assert.That(second.Produced.Select(total => total.Key.Value + "=" + total.Value), Is.EqualTo(new[] { "wood=2" }));
            Assert.That((city.Clock, city.GetQuantity(new ItemId("wood")), second.ProductionSeconds), Is.EqualTo((31L, 6L, 25L)));
        }

        [Test]
        public void StallReason_HutIdleCampAndFullStorage_GiveTheApiText()
        {
            GameContent content = SmallContent();
            var hut = new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var idleCamp = new Building(2, new BuildingTypeId("camp"), 1, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var staffedCamp = new Building(3, new BuildingTypeId("camp"), 2, 0, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            var stock = new List<KeyValuePair<ItemId, long>> { new KeyValuePair<ItemId, long>(new ItemId("wood"), 50) };
            var city = new CityState(100, 1, 4, stock, new[] { hut, idleCamp, staffedCamp }, System.Array.Empty<ResearchId>(), null);

            string?[] reasons = city.Buildings.Select(building => StallReason.Of(city, content, building)?.ToString()).ToArray();

            Assert.That(reasons, Is.EqualTo(new[] { "NoRecipe", "NoWorkers", "StorageFull:wood" }));
        }

        // One era, a capped and an uncapped item, a house and a producer; the numbers are made up for this test.
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
                    BaseStorageCaps = new Dictionary<string, long?> { ["wood"] = 50 },
                    MaxOfflineProductionSeconds = 600,
                    MinCycleSeconds = 2,
                    MaxLaborReductionPermille = 300,
                },
                StartingCity = new StartingCityData { Era = 1, Inventory = new Dictionary<string, long?> { ["wood"] = 4, ["gold"] = 20 } },
                Eras = new List<EraData?> { new EraData { Number = 1, Id = "first_era", Name = "First Era" } },
                Items = new List<ItemData?>
                {
                    new ItemData { Id = "wood", Name = "Wood", Era = 1, Capped = true, Tradable = true },
                    new ItemData { Id = "gold", Name = "Gold", Era = 1, Capped = false, Tradable = false },
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
                            },
                        },
                    },
                },
                Research = new List<ResearchData?>(),
            };

            GameContentResult result = GameContentFactory.Create(data);
            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors.Select(error => error.ToString())));

            // IsValid was asserted just above, so Content is set.
            return result.Content!;
        }
    }
}
