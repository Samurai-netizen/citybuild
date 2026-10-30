#nullable enable

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The one place that knows how the client reads and writes the API's JSON (<c>05</c>): camelCase member names, enums as their
    /// wire strings, quantities as <see cref="long"/>, and text kept as text (no date guessing). Every <see cref="ApiClient"/> owns one,
    /// so no serializer setting is shared through a static.
    /// </summary>
    public sealed class ApiJson
    {
        /// <summary>
        /// The nesting depth after which reading stops. The real answers are 5 levels deep; the limit keeps a hostile or broken body
        /// from exhausting the stack.
        /// </summary>
        private const int MaxDepth = 32;

        /// <summary>The settings every call of this instance uses; never changed after the constructor.</summary>
        private readonly JsonSerializerSettings _settings;

        /// <summary>Creates the serializer with the API's conventions.</summary>
        public ApiJson()
        {
            _settings = new JsonSerializerSettings
            {
                // Dictionary keys are item ids such as "trade_charter": they are data, so they keep their spelling.
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: false),
                },
                Converters = { new StringEnumConverter() },
                // Timestamps are kept as the server wrote them; a DateTimeOffset member is parsed from its own text only.
                DateParseHandling = DateParseHandling.None,
                MaxDepth = MaxDepth,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                TypeNameHandling = TypeNameHandling.None,
            };
        }

        /// <summary>The JSON text of <paramref name="value"/> for a request body.</summary>
        /// <param name="value">A request DTO.</param>
        public string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, _settings);
        }

        /// <summary>Reads <paramref name="json"/> as <typeparamref name="T"/>.</summary>
        /// <param name="json">The response body.</param>
        /// <returns>The object, or null when the text is the JSON <c>null</c>.</returns>
        /// <exception cref="JsonException">The text is not JSON, has the wrong shape, lacks a required member or has an unknown enum value.</exception>
        public T? Deserialize<T>(string json)
            where T : class
        {
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }
    }
}
