using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>The content DTOs (<c>05</c> Content) against a real <c>GET /content</c> answer, <c>Tests/Fixtures/content.json</c>.</summary>
    public sealed class ContentDtoTests
    {
        /// <summary>The reader and writer under test.</summary>
        private readonly ApiJson _json = new ApiJson();

        /// <summary>The answer of <c>/content</c> reads and writes back to the same text, byte for byte, nulls included (a house's recipe, level 1's upgrade cost, an effect's unused members).</summary>
        [Test]
        public void TheContentFixture_ReadsAndWritesBackToTheSameText()
        {
            string text = Fixtures.Read("content.json");

            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(text);

            Assert.That(content, Is.Not.Null);
            Assert.That(_json.Serialize(content), Is.EqualTo(text));
        }

        /// <summary>The rules and the starter city carry the numbers of the content file; quantities are <see cref="long"/>.</summary>
        [Test]
        public void TheRulesAndTheStartingCity_AreRead()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            Assert.That(content.ContentVersion, Is.EqualTo("mvp-2"));
            Assert.That(content.Rules.GridWidth, Is.EqualTo(8));
            Assert.That(content.Rules.TimeScale, Is.EqualTo(1));
            Assert.That(content.Rules.TripCarryLimit, Is.EqualTo(10L));
            Assert.That((content.Rules.MaxRouteStops, content.Rules.MaxStopActions), Is.EqualTo((8, 4)));
            Assert.That(content.StartingCity.Purse, Is.EqualTo(800L));
            Assert.That(content.Items.Find(item => item.Id == "coins").Currency, Is.True);
            Assert.That(content.Items.Find(item => item.Id == "logs").Currency, Is.False);
            Assert.That(content.AmountPresets.ConvertAll(preset => preset.Id + "=" + preset.Percent), Is.EqualTo(new[] { "all=100", "half=50", "quarter=25" }));
            Assert.That(content.Eras, Is.Not.Empty);
            Assert.That(content.Items, Is.Not.Empty);
        }

        /// <summary>
        /// Buildings: kinds read as enums, a house has no recipe or store, a producer has both, level 1 has no upgrade cost, a gated type names its
        /// research, the science building has its limit and a courier station its carry limit and stamina (CR-0008).
        /// </summary>
        [Test]
        public void TheBuildings_ReadKindsRecipesAndNulls()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            ContentBuildingDto house = content.Buildings.Find(b => b.Id == "house");
            ContentBuildingDto sawmill = content.Buildings.Find(b => b.Id == "sawmill");
            ContentBuildingDto post = content.Buildings.Find(b => b.Id == "trading_post");
            Assert.That(house.Kind, Is.EqualTo(BuildingKindDto.House));
            Assert.That(house.Levels[0].Recipe, Is.Null);
            Assert.That(sawmill.Kind, Is.EqualTo(BuildingKindDto.Producer));
            Assert.That(sawmill.UnlockedBy, Is.Null);
            Assert.That(sawmill.Levels[0].Recipe.Inputs["logs"], Is.EqualTo(2L));
            Assert.That(sawmill.Levels[0].Recipe.Labor, Is.EqualTo(180L));
            Assert.That(sawmill.Levels[0].UpgradeCost, Is.Null);
            Assert.That(sawmill.Levels[0].UpgradeSeconds, Is.Null);
            Assert.That(post.UnlockedBy, Is.EqualTo("trade_charter"));
            Assert.That(post.AvailableAtStart, Is.False);
            Assert.That(house.Levels[0].Storage, Is.Null);
            Assert.That(house.MaxPerCity, Is.Null);
            Assert.That(sawmill.Levels[0].Storage["planks"], Is.EqualTo(20L));
            Assert.That(sawmill.Levels[0].CarryLimit, Is.Null);
            ContentBuildingDto scriptorium = content.Buildings.Find(b => b.Id == "scriptorium");
            ContentBuildingDto station = content.Buildings.Find(b => b.Id == "courier_station");
            ContentBuildingDto warehouse = content.Buildings.Find(b => b.Id == "warehouse");
            Assert.That((scriptorium.Kind, scriptorium.MaxPerCity), Is.EqualTo((BuildingKindDto.Science, (int?)1)));
            Assert.That(station.Kind, Is.EqualTo(BuildingKindDto.CourierStation));
            Assert.That(station.Levels[0].CarryLimit, Is.GreaterThan(0L));
            Assert.That(station.Levels[0].StaminaSeconds, Is.GreaterThan(0L));
            Assert.That(warehouse.Kind, Is.EqualTo(BuildingKindDto.Warehouse));
            Assert.That(warehouse.Levels[0].Storage["logs"], Is.EqualTo(100L));
        }

        /// <summary>Research: every effect type of the content reads, and only the members its type uses are set.</summary>
        [Test]
        public void TheResearch_ReadsEveryEffectType()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            ContentResearchEffectDto unlock = content.Research.Find(r => r.Id == "trade_charter").Effects[0];
            ContentResearchEffectDto labor = content.Research.Find(r => r.Id == "sharp_axes").Effects[0];
            ContentResearchEffectDto storage = content.Research.Find(r => r.Id == "storehouses").Effects[0];
            Assert.That(unlock.Type, Is.EqualTo(ResearchEffectTypeDto.UnlockBuilding));
            Assert.That(unlock.Building, Is.EqualTo("trading_post"));
            Assert.That(unlock.Permille, Is.Null);
            Assert.That(labor.Type, Is.EqualTo(ResearchEffectTypeDto.LaborReduction));
            Assert.That(labor.Permille, Is.EqualTo(200));
            Assert.That(storage.Type, Is.EqualTo(ResearchEffectTypeDto.StorageCapBonus));
            Assert.That(storage.Item, Is.EqualTo("logs"));
            Assert.That(storage.Amount, Is.EqualTo(40L));
            ContentResearchEffectDto mode = content.Research.Find(r => r.Id == "tally_sticks").Effects[0];
            ContentResearchEffectDto walk = content.Research.Find(r => r.Id == "sturdy_boots").Effects[0];
            Assert.That((mode.Type, mode.Mode, mode.Item), Is.EqualTo((ResearchEffectTypeDto.UnlockAmountMode, "percent", (string)null)));
            Assert.That((walk.Type, walk.Permille, walk.Mode), Is.EqualTo((ResearchEffectTypeDto.WalkSpeedBonus, (int?)200, (string)null)));
            Assert.That(content.Research.Find(r => r.Id == "frame_saw").Prerequisites, Is.EqualTo(new[] { "sharp_axes" }));
        }
    }
}
