#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The starter city of <c>GET /content</c>; the content factory needs it.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentStartingCityDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentStartingCityDto(int era, long purse)
        {
            Era = era;
            Purse = purse;
        }

        /// <summary>The era a new city starts in.</summary>
        public int Era { get; }

        /// <summary>The coins a new city starts with; it has no goods (CR-0008 D2).</summary>
        public long Purse { get; }
    }
}
