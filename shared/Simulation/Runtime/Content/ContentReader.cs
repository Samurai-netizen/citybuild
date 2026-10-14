#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The shape phase of <see cref="GameContentFactory.Create"/>: turns the data into definitions and reports every value that is missing,
    /// malformed or not allowed where it is. Rules about the values themselves (signs, references, levels, the research graph) come after.
    /// </summary>
    internal sealed class ContentReader
    {
        private const string _kindHouse = "house";
        private const string _kindProducer = "producer";
        private const string _effectUnlockBuilding = "unlockBuilding";
        private const string _effectLaborReduction = "laborReduction";
        private const string _effectStorageCapBonus = "storageCapBonus";

        private readonly ContentErrorList _errors;
        private readonly FieldReader _fields;

        public ContentReader(ContentErrorList errors)
        {
            _errors = errors;
            _fields = new FieldReader(errors);
        }

        /// <summary>The draft, or null when the data had shape errors (all of them are in the error list).</summary>
        public ContentDraft? Read(GameContentData data)
        {
            string version = _fields.RequireText(data.ContentVersion, "contentVersion");
            GameRules? rules = data.Rules is null ? MissingObject<GameRules>("rules") : ReadRules(data.Rules, "rules");
            StartingCityDefinition? startingCity = data.StartingCity is null
                ? MissingObject<StartingCityDefinition>("startingCity")
                : ReadStartingCity(data.StartingCity, "startingCity");
            List<EraDefinition> eras = _fields.ReadList(data.Eras, "eras", true, era => era.Id, ReadEra);
            List<ItemDefinition> items = _fields.ReadList(data.Items, "items", true, item => item.Id, ReadItem);
            List<ResearchDefinition> research = _fields.ReadList(data.Research, "research", true, node => node.Id, ReadResearch);
            Dictionary<BuildingTypeId, ResearchId> unlocks = FirstUnlockingResearch(research);
            List<BuildingDefinition> buildings = _fields.ReadList(
                data.Buildings, "buildings", true, building => building.Id, (building, path) => ReadBuilding(building, path, unlocks));

            if (_errors.Count > 0 || rules is null || startingCity is null)
            {
                return null;
            }

            return new ContentDraft(version, rules, startingCity, eras, items, buildings, research);
        }

        private T? MissingObject<T>(string path)
            where T : class
        {
            _fields.Missing(path);
            return null;
        }

        private GameRules ReadRules(GameRulesData data, string path) =>
            new GameRules(
                _fields.RequireInt(data.GridWidth, path + ".gridWidth"),
                _fields.RequireInt(data.GridHeight, path + ".gridHeight"),
                _fields.RequireInt(data.BuilderSlots, path + ".builderSlots"),
                _fields.RequireInt(data.ResearchSlots, path + ".researchSlots"),
                _fields.ReadItemAmounts(data.BaseStorageCaps, path + ".baseStorageCaps", true),
                _fields.RequireLong(data.MaxOfflineProductionSeconds, path + ".maxOfflineProductionSeconds"),
                _fields.RequireLong(data.MinCycleSeconds, path + ".minCycleSeconds"),
                _fields.RequireInt(data.MaxLaborReductionPermille, path + ".maxLaborReductionPermille"));

        private StartingCityDefinition ReadStartingCity(StartingCityData data, string path) =>
            new StartingCityDefinition(
                _fields.RequireInt(data.Era, path + ".era"),
                _fields.ReadItemAmounts(data.Inventory, path + ".inventory", true));

        private EraDefinition? ReadEra(EraData data, string path)
        {
            int number = _fields.RequireInt(data.Number, path + ".number");
            EraId? id = _fields.RequireId(data.Id, path + ".id", text => new EraId(text));
            string name = _fields.RequireText(data.Name, path + ".name");
            return id.HasValue ? new EraDefinition(number, id.Value, name) : null;
        }

        private ItemDefinition? ReadItem(ItemData data, string path)
        {
            ItemId? id = _fields.RequireId(data.Id, path + ".id", text => new ItemId(text));
            string name = _fields.RequireText(data.Name, path + ".name");
            int era = _fields.RequireInt(data.Era, path + ".era");
            bool capped = _fields.RequireBool(data.Capped, path + ".capped");
            bool tradable = _fields.RequireBool(data.Tradable, path + ".tradable");
            return id.HasValue ? new ItemDefinition(id.Value, name, era, capped, tradable) : null;
        }

        private ResearchDefinition? ReadResearch(ResearchData data, string path)
        {
            ResearchId? id = _fields.RequireId(data.Id, path + ".id", text => new ResearchId(text));
            string name = _fields.RequireText(data.Name, path + ".name");
            int era = _fields.RequireInt(data.Era, path + ".era");
            IReadOnlyList<ItemQuantity> cost = _fields.ReadItemAmounts(data.Cost, path + ".cost", true);
            long duration = _fields.RequireLong(data.DurationSeconds, path + ".durationSeconds");
            List<ResearchId> prerequisites = ReadPrerequisites(data.Prerequisites, path + ".prerequisites");
            List<ResearchEffect> effects = _fields.ReadList(data.Effects, path + ".effects", false, _ => null, ReadEffect);
            if (!id.HasValue)
            {
                return null;
            }

            IReadOnlyList<ResearchId> sortedPrerequisites = ReadOnlyLists.Sorted(prerequisites, (left, right) => left.CompareTo(right));
            return new ResearchDefinition(id.Value, name, era, cost, duration, sortedPrerequisites, ReadOnlyLists.From(effects));
        }

        private List<ResearchId> ReadPrerequisites(List<string?>? texts, string path)
        {
            var prerequisites = new List<ResearchId>();
            for (int index = 0; texts is not null && index < texts.Count; index++)
            {
                ResearchId? prerequisite = _fields.RequireId(texts[index], path + "[#" + index + "]", text => new ResearchId(text));
                if (prerequisite.HasValue)
                {
                    prerequisites.Add(prerequisite.Value);
                }
            }

            return prerequisites;
        }

        private ResearchEffect? ReadEffect(ResearchEffectData data, string path)
        {
            switch (data.Type)
            {
                case null:
                    _fields.Missing(path + ".type");
                    return null;
                case _effectUnlockBuilding:
                    AllowOnlyEffectFields(data, path, building: true, item: false, permille: false, amount: false);
                    BuildingTypeId? unlocked = _fields.RequireId(data.Building, path + ".building", text => new BuildingTypeId(text));
                    return unlocked.HasValue ? new UnlockBuildingEffect(unlocked.Value) : null;
                case _effectLaborReduction:
                    AllowOnlyEffectFields(data, path, building: true, item: false, permille: true, amount: false);
                    BuildingTypeId? reduced = _fields.RequireId(data.Building, path + ".building", text => new BuildingTypeId(text));
                    int permille = _fields.RequireInt(data.Permille, path + ".permille");
                    return reduced.HasValue ? new LaborReductionEffect(reduced.Value, permille) : null;
                case _effectStorageCapBonus:
                    AllowOnlyEffectFields(data, path, building: false, item: true, permille: false, amount: true);
                    ItemId? item = _fields.RequireId(data.Item, path + ".item", text => new ItemId(text));
                    long amount = _fields.RequireLong(data.Amount, path + ".amount");
                    return item.HasValue ? new StorageCapBonusEffect(item.Value, amount) : null;
                default:
                    _fields.InvalidValue(
                        path + ".type",
                        $"'{data.Type}' is not an effect type: use '{_effectUnlockBuilding}', '{_effectLaborReduction}' or '{_effectStorageCapBonus}'.");
                    return null;
            }
        }

        // Each effect type has its own values; a value of another type is a mistake (a typo, or a half-changed effect), never ignored.
        private void AllowOnlyEffectFields(ResearchEffectData data, string path, bool building, bool item, bool permille, bool amount)
        {
            string reason = $"An effect of type '{data.Type}' has no such value.";
            _fields.Unexpected(!building && data.Building is not null, path + ".building", reason);
            _fields.Unexpected(!item && data.Item is not null, path + ".item", reason);
            _fields.Unexpected(!permille && data.Permille is not null, path + ".permille", reason);
            _fields.Unexpected(!amount && data.Amount is not null, path + ".amount", reason);
        }

        private BuildingDefinition? ReadBuilding(BuildingData data, string path, Dictionary<BuildingTypeId, ResearchId> unlocks)
        {
            BuildingTypeId? id = _fields.RequireId(data.Id, path + ".id", text => new BuildingTypeId(text));
            string name = _fields.RequireText(data.Name, path + ".name");
            BuildingKind kind = ReadKind(data.Kind, path + ".kind");
            int era = _fields.RequireInt(data.Era, path + ".era");
            bool availableAtStart = _fields.RequireBool(data.AvailableAtStart, path + ".availableAtStart");
            IReadOnlyList<ItemQuantity> cost = _fields.ReadItemAmounts(data.Cost, path + ".cost", true);
            long buildSeconds = _fields.RequireLong(data.BuildSeconds, path + ".buildSeconds");
            List<BuildingLevelDefinition> levels = _fields.ReadList(
                data.Levels, path + ".levels", true, level => level.Level?.ToString(CultureInfo.InvariantCulture), ReadLevel);
            if (!id.HasValue)
            {
                return null;
            }

            ResearchId? unlockedBy = !availableAtStart && unlocks.TryGetValue(id.Value, out ResearchId research) ? research : (ResearchId?)null;
            IReadOnlyList<BuildingLevelDefinition> sortedLevels = ReadOnlyLists.From(levels.OrderBy(level => level.Level));
            return new BuildingDefinition(id.Value, name, kind, era, cost, buildSeconds, availableAtStart, unlockedBy, sortedLevels);
        }

        private BuildingKind ReadKind(string? text, string path)
        {
            switch (text)
            {
                case null:
                    _fields.Missing(path);
                    return BuildingKind.House;
                case _kindHouse:
                    return BuildingKind.House;
                case _kindProducer:
                    return BuildingKind.Producer;
                default:
                    _fields.InvalidValue(path, $"'{text}' is not a building kind: use '{_kindHouse}' or '{_kindProducer}'.");
                    return BuildingKind.House;
            }
        }

        private BuildingLevelDefinition ReadLevel(BuildingLevelData data, string path)
        {
            int level = _fields.RequireInt(data.Level, path + ".level");
            RecipeDefinition? recipe = data.Recipe is null ? null : ReadRecipe(data.Recipe, path + ".recipe");
            IReadOnlyList<ItemQuantity> upgradeCost = ReadOnlyLists.From(Array.Empty<ItemQuantity>());
            long upgradeSeconds = 0;
            if (data.Level == 1)
            {
                const string reason = "Level 1 is built, not upgraded to, so it has no upgrade cost or time; the building's cost and buildSeconds apply.";
                _fields.Unexpected(data.UpgradeCost is not null, path + ".upgradeCost", reason);
                _fields.Unexpected(data.UpgradeSeconds is not null, path + ".upgradeSeconds", reason);
            }
            else if (data.Level > 1)
            {
                upgradeCost = _fields.ReadItemAmounts(data.UpgradeCost, path + ".upgradeCost", true);
                upgradeSeconds = _fields.RequireLong(data.UpgradeSeconds, path + ".upgradeSeconds");
            }

            return new BuildingLevelDefinition(level, data.MaxWorkers ?? 0, data.WorkersProvided ?? 0, recipe, upgradeCost, upgradeSeconds);
        }

        private RecipeDefinition ReadRecipe(RecipeData data, string path) =>
            new RecipeDefinition(
                _fields.ReadItemAmounts(data.Inputs, path + ".inputs", false),
                _fields.ReadItemAmounts(data.Outputs, path + ".outputs", true),
                _fields.RequireLong(data.Labor, path + ".labor"));

        // The building's UnlockedByResearch comes from the research effects, the only place the unlock is stated. The first node in file
        // order wins; when more than one unlocks a type, the rule phase reports it and this content never becomes valid.
        private static Dictionary<BuildingTypeId, ResearchId> FirstUnlockingResearch(List<ResearchDefinition> research)
        {
            var unlocks = new Dictionary<BuildingTypeId, ResearchId>();
            foreach (ResearchDefinition node in research)
            {
                foreach (UnlockBuildingEffect unlock in node.Effects.OfType<UnlockBuildingEffect>())
                {
                    unlocks.TryAdd(unlock.Building, node.Id);
                }
            }

            return unlocks;
        }
    }
}
