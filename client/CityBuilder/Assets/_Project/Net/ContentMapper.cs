#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Turns the <c>GET /content</c> answer into the engine's <see cref="GameContentData"/>, the input of the validating
    /// <c>GameContentFactory</c> (<c>05</c> Content: "Complete for the client"). It copies; it checks nothing, because the factory is the one
    /// place that decides whether content is valid, on the server and here alike. The one change of spelling is the building kind: the
    /// response says <c>House</c> and <c>Producer</c>, the content file <c>house</c> and <c>producer</c>. The response's extra members
    /// (<c>unlockedBy</c>, <c>timeScale</c>) have no place in the data and are left out.
    /// </summary>
    public static class ContentMapper
    {
        /// <summary>The kind spelling of a house in the content file.</summary>
        private const string HouseKind = "house";

        /// <summary>The kind spelling of a producer in the content file.</summary>
        private const string ProducerKind = "producer";

        /// <summary>Maps the whole answer.</summary>
        /// <param name="response">The decoded answer; its members are all set (the DTOs require them).</param>
        /// <returns>The data for <c>GameContentFactory.Create</c>.</returns>
        public static GameContentData ToData(ContentResponseDto response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            return new GameContentData
            {
                ContentVersion = response.ContentVersion,
                Rules = ToData(response.Rules),
                StartingCity = new StartingCityData { Era = response.StartingCity.Era, Inventory = Quantities(response.StartingCity.Inventory) },
                Eras = response.Eras.ConvertAll(era => (EraData?)new EraData { Number = era.Number, Id = era.Id, Name = era.Name }),
                Items = response.Items.ConvertAll(item => (ItemData?)ToData(item)),
                Buildings = response.Buildings.ConvertAll(building => (BuildingData?)ToData(building)),
                Research = response.Research.ConvertAll(research => (ResearchData?)ToData(research)),
            };
        }

        /// <summary>The global rules; <c>timeScale</c> is the server clock's speed and is not content.</summary>
        private static GameRulesData ToData(ContentRulesDto rules)
        {
            return new GameRulesData
            {
                GridWidth = rules.GridWidth,
                GridHeight = rules.GridHeight,
                BuilderSlots = rules.BuilderSlots,
                ResearchSlots = rules.ResearchSlots,
                BaseStorageCaps = Quantities(rules.BaseStorageCaps),
                MaxOfflineProductionSeconds = rules.MaxOfflineProductionSeconds,
                MinCycleSeconds = rules.MinCycleSeconds,
                MaxLaborReductionPermille = rules.MaxLaborReductionPermille,
            };
        }

        /// <summary>One item.</summary>
        private static ItemData ToData(ContentItemDto item)
        {
            return new ItemData { Id = item.Id, Name = item.Name, Era = item.Era, Capped = item.Capped, Tradable = item.Tradable };
        }

        /// <summary>One building type with its levels.</summary>
        private static BuildingData ToData(ContentBuildingDto building)
        {
            return new BuildingData
            {
                Id = building.Id,
                Name = building.Name,
                Kind = KindText(building.Kind),
                Era = building.Era,
                AvailableAtStart = building.AvailableAtStart,
                Cost = Quantities(building.Cost),
                BuildSeconds = building.BuildSeconds,
                Levels = building.Levels.ConvertAll(level => (BuildingLevelData?)ToData(level)),
            };
        }

        /// <summary>One building level; a house has no recipe and level 1 has no upgrade cost, so those stay null.</summary>
        private static BuildingLevelData ToData(ContentBuildingLevelDto level)
        {
            return new BuildingLevelData
            {
                Level = level.Level,
                MaxWorkers = level.MaxWorkers,
                WorkersProvided = level.WorkersProvided,
                Recipe = level.Recipe == null
                    ? null
                    : new RecipeData { Inputs = Quantities(level.Recipe.Inputs), Outputs = Quantities(level.Recipe.Outputs), Labor = level.Recipe.Labor },
                UpgradeCost = level.UpgradeCost == null ? null : Quantities(level.UpgradeCost),
                UpgradeSeconds = level.UpgradeSeconds,
            };
        }

        /// <summary>One research node with its effects.</summary>
        private static ResearchData ToData(ContentResearchDto research)
        {
            return new ResearchData
            {
                Id = research.Id,
                Name = research.Name,
                Era = research.Era,
                Cost = Quantities(research.Cost),
                DurationSeconds = research.DurationSeconds,
                Prerequisites = research.Prerequisites.ConvertAll(prerequisite => (string?)prerequisite),
                Effects = research.Effects.ConvertAll(effect => (ResearchEffectData?)ToData(effect)),
            };
        }

        /// <summary>One effect; members its type does not use stay null, as the response writes them.</summary>
        private static ResearchEffectData ToData(ContentResearchEffectDto effect)
        {
            return new ResearchEffectData
            {
                Type = EffectTypeText(effect.Type),
                Building = effect.Building,
                Item = effect.Item,
                Permille = effect.Permille,
                Amount = effect.Amount,
            };
        }

        /// <summary>A copy of an item-to-quantity map with nullable values, the shape the data classes use.</summary>
        private static Dictionary<string, long?> Quantities(Dictionary<string, long> quantities)
        {
            var copy = new Dictionary<string, long?>(quantities.Count);
            foreach (KeyValuePair<string, long> pair in quantities)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }

        /// <summary>The content file's spelling of a kind.</summary>
        private static string KindText(BuildingKindDto kind)
        {
            switch (kind)
            {
                case BuildingKindDto.House:
                    return HouseKind;
                case BuildingKindDto.Producer:
                    return ProducerKind;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "A building kind the client does not know.");
            }
        }

        /// <summary>The content file's spelling of an effect type (the same as the response's).</summary>
        private static string EffectTypeText(ResearchEffectTypeDto type)
        {
            switch (type)
            {
                case ResearchEffectTypeDto.UnlockBuilding:
                    return "unlockBuilding";
                case ResearchEffectTypeDto.LaborReduction:
                    return "laborReduction";
                case ResearchEffectTypeDto.StorageCapBonus:
                    return "storageCapBonus";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "An effect type the client does not know.");
            }
        }
    }
}
