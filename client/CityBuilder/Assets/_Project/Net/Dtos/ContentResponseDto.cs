#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The answer of <c>GET /api/v1/content</c> (<c>05</c> Content): all game numbers, the same for every player. The client builds its <c>GameContent</c> from it and never keeps numbers of its own.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentResponseDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentResponseDto(string contentVersion, ContentRulesDto rules, ContentStartingCityDto startingCity, List<ContentEraDto> eras, List<ContentItemDto> items, List<ContentBuildingDto> buildings, List<ContentResearchDto> research)
        {
            ContentVersion = contentVersion;
            Rules = rules;
            StartingCity = startingCity;
            Eras = eras;
            Items = items;
            Buildings = buildings;
            Research = research;
        }

        /// <summary>The version of the numbers.</summary>
        public string ContentVersion { get; }

        /// <summary>The game rules.</summary>
        public ContentRulesDto Rules { get; }

        /// <summary>The starter city.</summary>
        public ContentStartingCityDto StartingCity { get; }

        /// <summary>The eras, by number.</summary>
        public List<ContentEraDto> Eras { get; }

        /// <summary>The items, by id.</summary>
        public List<ContentItemDto> Items { get; }

        /// <summary>The building types, by id.</summary>
        public List<ContentBuildingDto> Buildings { get; }

        /// <summary>The research nodes, by id.</summary>
        public List<ContentResearchDto> Research { get; }
    }
}
