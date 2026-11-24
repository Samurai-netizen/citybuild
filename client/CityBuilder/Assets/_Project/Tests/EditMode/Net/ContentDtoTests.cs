using System.Collections.Generic;
using System.Linq;
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

        /// <summary>The M0 answer the snapshot fixtures go with (no queue, no base max level) still reads and writes back byte for byte (decision 14).</summary>
        [Test]
        public void TheM0ContentFixture_ReadsAndWritesBackToTheSameText()
        {
            string text = Fixtures.Read("content-mvp-2.json");

            Assert.That(_json.Serialize(_json.Deserialize<ContentResponseDto>(text)), Is.EqualTo(text));
        }

        /// <summary>The rules and the starter city carry the numbers of the content file; quantities are <see cref="long"/>.</summary>
        [Test]
        public void TheRulesAndTheStartingCity_AreRead()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            Assert.That(content.ContentVersion, Is.EqualTo("era1-2"));
            Assert.That((content.Rules.GridWidth, content.Rules.GridHeight), Is.EqualTo((18, 14)));
            Assert.That(content.Rules.TimeScale, Is.EqualTo(1));
            Assert.That(content.Rules.TripCarryLimit, Is.EqualTo(10L));
            Assert.That((content.Rules.MaxRouteStops, content.Rules.MaxStopActions), Is.EqualTo((8, 4)));
            Assert.That(content.StartingCity.Purse, Is.EqualTo(1000L));
            Assert.That(content.Rules.BuilderQueueLength, Is.EqualTo(2));
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

        /// <summary>The three effects of Era I (M1-02) read: a level cap rise for a type, a builder slot, and the charter, each with only its members set.</summary>
        [Test]
        public void TheResearch_ReadsTheEraIEffects()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            ContentResearchEffectDto level = content.Research.Find(r => r.Id == "timber_framing").Effects[0];
            ContentResearchEffectDto slot = content.Research.Find(r => r.Id == "guild_of_masons").Effects[0];
            ContentResearchEffectDto charter = content.Research.Find(r => r.Id == "town_charter").Effects[0];
            Assert.That((level.Type, level.Building, level.Amount), Is.EqualTo((ResearchEffectTypeDto.MaxLevelBonus, "house", (long?)1)));
            Assert.That((slot.Type, slot.Building, slot.Amount), Is.EqualTo((ResearchEffectTypeDto.BuilderSlotBonus, (string)null, (long?)1)));
            Assert.That((charter.Type, charter.Building, charter.Amount), Is.EqualTo((ResearchEffectTypeDto.GrantCharter, (string)null, (long?)null)));
        }

        /// <summary>
        /// The map (M1-03) reads: 14 rows of 18 terrain letters and 18 area letters, the start area and the rough tile's walk; the river is
        /// column 12, and the start area A is the middle of the map.
        /// </summary>
        [Test]
        public void TheMap_IsRead()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            ContentMapDto map = content.Map!;
            Assert.That((map.Id, map.StartArea, map.RoughTileMillis), Is.EqualTo(("crossing", "A", 10000L)));
            Assert.That(map.Tiles, Has.Count.EqualTo(14).And.All.Length.EqualTo(18));
            Assert.That(map.Areas, Has.Count.EqualTo(14).And.All.Length.EqualTo(18));
            Assert.That((map.Tiles[5][12], map.Areas[5][12], map.Areas[0][0], map.Areas[0][17]), Is.EqualTo(('W', 'A', 'B', 'C')));
        }

        /// <summary>The bridge (M1-03) reads: its name, its walk per tile and its cost per tile.</summary>
        [Test]
        public void TheBridge_IsRead()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            ContentBridgeDto bridge = content.Bridge!;
            Assert.That((bridge.Name, bridge.TileMillis), Is.EqualTo(("Bridge", 1500L)));
            Assert.That(bridge.Cost, Is.EqualTo(new Dictionary<string, long> { ["coins"] = 60 }));
        }

        /// <summary>A building type's site (M1-03) reads: the three raw producers need their terrain, every other type has none.</summary>
        [Test]
        public void TheBuildings_ReadTheirSite()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            Assert.That(content.Buildings.Find(b => b.Id == "woodcutter").Site, Is.EqualTo(BuildingSiteDto.NearForest));
            Assert.That(content.Buildings.Find(b => b.Id == "quarry").Site, Is.EqualTo(BuildingSiteDto.NearRock));
            Assert.That(content.Buildings.Find(b => b.Id == "farm").Site, Is.EqualTo(BuildingSiteDto.FertileSoil));
            Assert.That(content.Buildings.Where(b => b.Site != null).Select(b => b.Id), Is.EquivalentTo(new[] { "woodcutter", "quarry", "farm" }));
        }

        /// <summary>The map's two effects (M1-03) read: a research opens an area by its letter, another allows bridges; other effects have no area.</summary>
        [Test]
        public void TheResearch_ReadsTheMapEffects()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            ContentResearchEffectDto survey = content.Research.Find(r => r.Id == "land_survey").Effects[0];
            ContentResearchEffectDto fields = content.Research.Find(r => r.Id == "open_fields").Effects.Find(e => e.Type == ResearchEffectTypeDto.OpenLand);
            ContentResearchEffectDto bridge = content.Research.Find(r => r.Id == "bridge_building").Effects[0];
            Assert.That((survey.Type, survey.Area), Is.EqualTo((ResearchEffectTypeDto.OpenLand, "B")));
            Assert.That(fields.Area, Is.EqualTo("C"));
            Assert.That((bridge.Type, bridge.Area), Is.EqualTo((ResearchEffectTypeDto.UnlockBridge, (string)null)));
        }

        /// <summary>The M0 answer has no map, no bridge, no site and no area: each reads as <c>null</c>.</summary>
        [Test]
        public void TheM0Content_HasNoMapBridgeSiteOrArea()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content-mvp-2.json"));

            Assert.That((content.Map, content.Bridge), Is.EqualTo(((ContentMapDto)null, (ContentBridgeDto)null)));
            Assert.That(content.Buildings.All(b => b.Site == null), Is.True);
            Assert.That(content.Research.SelectMany(r => r.Effects).All(e => e.Area == null), Is.True);
        }

        /// <summary>A building type's <c>baseMaxLevel</c> (M1-02) reads: the cottage stops at 2 without research, the Town Hall at 3.</summary>
        [Test]
        public void TheBuildings_ReadTheirBaseMaxLevel()
        {
            ContentResponseDto content = _json.Deserialize<ContentResponseDto>(Fixtures.Read("content.json"));

            Assert.That(content.Buildings.Find(b => b.Id == "house").BaseMaxLevel, Is.EqualTo(2));
            Assert.That(content.Buildings.Find(b => b.Id == "town_hall").BaseMaxLevel, Is.EqualTo(3));
            Assert.That(content.Buildings.Find(b => b.Id == "scriptorium").BaseMaxLevel, Is.EqualTo(1));
        }
    }
}
