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
        /// <summary>The JSON value of <c>kind</c> for a house, which provides workers (<see cref="BuildingKind.House"/>).</summary>
        private const string _kindHouse = "house";
        /// <summary>The JSON value of <c>kind</c> for a producer, which runs a recipe (<see cref="BuildingKind.Producer"/>).</summary>
        private const string _kindProducer = "producer";
        /// <summary>The JSON value of an effect's <c>type</c> that unlocks a building type (<see cref="UnlockBuildingEffect"/>).</summary>
        private const string _effectUnlockBuilding = "unlockBuilding";
        /// <summary>The JSON value of an effect's <c>type</c> that reduces a producer's labor (<see cref="LaborReductionEffect"/>).</summary>
        private const string _effectLaborReduction = "laborReduction";
        /// <summary>The JSON value of an effect's <c>type</c> that raises an item's storage cap (<see cref="StorageCapBonusEffect"/>).</summary>
        private const string _effectStorageCapBonus = "storageCapBonus";

        /// <summary>Where every shape error goes; shared with the rule phase that follows.</summary>
        private readonly ContentErrorList _errors;
        /// <summary>Reads the single values and reports their shape errors into <see cref="_errors"/>.</summary>
        private readonly FieldReader _fields;

        /// <summary>Creates a reader that reports into the factory's error list.</summary>
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

        /// <summary>Reports a missing required object (such as <c>rules</c>) and returns null for it.</summary>
        private T? MissingObject<T>(string path)
            where T : class
        {
            _fields.Missing(path);
            return null;
        }

        /// <summary>Reads the global rules; every value is required.</summary>
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

        /// <summary>Reads the starting city: its era number and stock (the map may be empty).</summary>
        private StartingCityDefinition ReadStartingCity(StartingCityData data, string path) =>
            new StartingCityDefinition(
                _fields.RequireInt(data.Era, path + ".era"),
                _fields.ReadItemAmounts(data.Inventory, path + ".inventory", true));

        /// <summary>Reads one era; null when its id is missing or malformed (reported), since it can't be named then.</summary>
        private EraDefinition? ReadEra(EraData data, string path)
        {
            int number = _fields.RequireInt(data.Number, path + ".number");
            EraId? id = _fields.RequireId(data.Id, path + ".id", text => new EraId(text));
            string name = _fields.RequireText(data.Name, path + ".name");
            return id.HasValue ? new EraDefinition(number, id.Value, name) : null;
        }

        /// <summary>Reads one item; null when its id is missing or malformed (reported).</summary>
        private ItemDefinition? ReadItem(ItemData data, string path)
        {
            ItemId? id = _fields.RequireId(data.Id, path + ".id", text => new ItemId(text));
            string name = _fields.RequireText(data.Name, path + ".name");
            int era = _fields.RequireInt(data.Era, path + ".era");
            bool capped = _fields.RequireBool(data.Capped, path + ".capped");
            bool tradable = _fields.RequireBool(data.Tradable, path + ".tradable");
            return id.HasValue ? new ItemDefinition(id.Value, name, era, capped, tradable) : null;
        }

        /// <summary>
        /// Reads one research node; null when its id is missing or malformed (reported). Prerequisites are sorted by id, so equal content
        /// compares equal whatever order the file lists them in; effects keep the file's order.
        /// </summary>
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

        /// <summary>Reads the prerequisite ids in file order, named by position (<c>prerequisites[#0]</c>); a malformed one is reported and left out.</summary>
        private List<ResearchId> ReadPrerequisites(List<string?>? texts, string path)
        {
            var prerequisites = new List<ResearchId>();
            for (int index = 0; texts is not null && index < texts.Count; index++)
            {
                ResearchId? prerequisite = _fields.RequireId(texts[index], path + "[#" + index.ToString(CultureInfo.InvariantCulture) + "]", text => new ResearchId(text));
                if (prerequisite.HasValue)
                {
                    prerequisites.Add(prerequisite.Value);
                }
            }

            return prerequisites;
        }

        /// <summary>
        /// Reads one effect by its <c>type</c>: each type has its own values, and a value of another type is an error. Null when the type is
        /// missing or unknown, or when its building or item id is missing or malformed (all reported); a missing number is reported and reads
        /// as 0, which the value rules then report too.
        /// </summary>
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

        /// <summary>
        /// Reports every value an effect of this type doesn't have: each effect type has its own values, and a value of another type is a
        /// mistake (a typo, or a half-changed effect), never silently ignored. The flags say which values the type has.
        /// </summary>
        private void AllowOnlyEffectFields(ResearchEffectData data, string path, bool building, bool item, bool permille, bool amount)
        {
            string reason = $"An effect of type '{data.Type}' has no such value.";
            _fields.Unexpected(!building && data.Building is not null, path + ".building", reason);
            _fields.Unexpected(!item && data.Item is not null, path + ".item", reason);
            _fields.Unexpected(!permille && data.Permille is not null, path + ".permille", reason);
            _fields.Unexpected(!amount && data.Amount is not null, path + ".amount", reason);
        }

        /// <summary>
        /// Reads one building type; null when its id is missing or malformed (reported). Its levels are sorted by number, and
        /// <see cref="BuildingDefinition.UnlockedByResearch"/> comes from <paramref name="unlocks"/> when the type isn't available at the start.
        /// </summary>
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

        /// <summary>Reads <c>kind</c>: <c>house</c> or <c>producer</c>. A missing or unknown value is reported, and the stand-in is <see cref="BuildingKind.House"/>.</summary>
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

        /// <summary>
        /// Reads one level. Level 1 is built, not upgraded to, so an upgrade cost or time there is an error; levels 2 and up need both. A
        /// missing <c>maxWorkers</c> or <c>workersProvided</c> reads as 0 here, and <see cref="BuildingRules"/> decides whether that kind needs it.
        /// </summary>
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

        /// <summary>Reads a recipe: inputs (optional, empty for a raw producer), outputs and labor (both required).</summary>
        private RecipeDefinition ReadRecipe(RecipeData data, string path) =>
            new RecipeDefinition(
                _fields.ReadItemAmounts(data.Inputs, path + ".inputs", false),
                _fields.ReadItemAmounts(data.Outputs, path + ".outputs", true),
                _fields.RequireLong(data.Labor, path + ".labor"));

        /// <summary>
        /// The research node that unlocks each building type, from the research effects, the only place an unlock is stated; this fills
        /// <see cref="BuildingDefinition.UnlockedByResearch"/>. The first node in file order wins; when more than one unlocks a type, the rule
        /// phase reports it and the content never becomes valid.
        /// </summary>
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
