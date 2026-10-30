#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>A producer's recipe at one level (<c>05</c> Content).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentRecipeDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentRecipeDto(Dictionary<string, long> inputs, Dictionary<string, long> outputs, long labor)
        {
            Inputs = inputs;
            Outputs = outputs;
            Labor = labor;
        }

        /// <summary>Item id to the quantity one cycle consumes.</summary>
        public Dictionary<string, long> Inputs { get; }

        /// <summary>Item id to the quantity one cycle produces.</summary>
        public Dictionary<string, long> Outputs { get; }

        /// <summary>Worker-seconds one cycle needs.</summary>
        public long Labor { get; }
    }
}
