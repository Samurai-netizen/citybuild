#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>Body of <c>POST /api/v1/commands/start-research</c> (<c>05</c> Commands).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class StartResearchRequestDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public StartResearchRequestDto(Guid requestId, string researchId)
        {
            RequestId = requestId;
            ResearchId = researchId;
        }

        /// <summary>The command's id, reused on every retry.</summary>
        public Guid RequestId { get; }

        /// <summary>The content id of the research node to start, for example <c>trade_charter</c>.</summary>
        public string ResearchId { get; }
    }
}
