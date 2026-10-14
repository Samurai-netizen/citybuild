#nullable enable
using System.Collections.Generic;
using System.Linq;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode
{
    /// <summary>
    /// Proves that the content factory of the local package runs on Unity's runtime the way it runs on .NET: valid data gives content, bad data
    /// gives every error in the same order. The rules themselves are tested in <c>server/tests/CityBuilder.Simulation.Tests/Content</c>.
    /// </summary>
    public sealed class ContentInUnityTests
    {
        [Test]
        public void Create_SmallValidContent_BuildsSortedContent()
        {
            GameContentResult result = GameContentFactory.Create(SmallContent());

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors.Select(error => error.ToString())));
            // IsValid was asserted just above, so Content is set; and TryGetBuilding returned true, so hut is set.
            Assert.That(result.Content!.Items.Select(item => item.Id.Value), Is.EqualTo(new[] { "gold", "wood" }));
            Assert.That(result.Content.TryGetBuilding(new BuildingTypeId("hut"), out BuildingDefinition? hut), Is.True);
            Assert.That(hut!.GetLevel(1).WorkersProvided, Is.EqualTo(2));
        }

        [Test]
        public void Create_TwoRuleErrors_ReportsBothInOrder()
        {
            // SmallContent sets every value that "!" reaches here.
            GameContentData data = SmallContent();
            data.Rules!.GridWidth = 0;
            data.Buildings![0]!.Cost!["gold"] = -1;

            GameContentResult result = GameContentFactory.Create(data);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Errors.Select(error => error.Code + " " + error.Path),
                Is.EqualTo(new[] { "NotPositive rules.gridWidth", "Negative buildings[hut].cost.gold" }));
        }

        // One era, a capped and an uncapped item, and a house; the numbers are made up for this test.
        private static GameContentData SmallContent() => new GameContentData
        {
            ContentVersion = "unity-test-1",
            Rules = new GameRulesData
            {
                GridWidth = 4,
                GridHeight = 4,
                BuilderSlots = 1,
                ResearchSlots = 1,
                BaseStorageCaps = new Dictionary<string, long?> { ["wood"] = 50 },
                MaxOfflineProductionSeconds = 600,
                MinCycleSeconds = 2,
                MaxLaborReductionPermille = 300,
            },
            StartingCity = new StartingCityData { Era = 1, Inventory = new Dictionary<string, long?> { ["gold"] = 20 } },
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
                    Id = "hut",
                    Name = "Hut",
                    Kind = "house",
                    Era = 1,
                    AvailableAtStart = true,
                    Cost = new Dictionary<string, long?> { ["gold"] = 5 },
                    BuildSeconds = 3,
                    Levels = new List<BuildingLevelData?> { new BuildingLevelData { Level = 1, WorkersProvided = 2 } },
                },
            },
            Research = new List<ResearchData?>(),
        };
    }
}
