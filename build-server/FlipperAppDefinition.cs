using Newtonsoft.Json;

namespace build_server
{
    public class FlipperAppDefinition
    {
        [JsonProperty("appid")]
        public string AppId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("apptype")]
        public string AppType { get; set; }

        [JsonProperty("fap_category")]
        public string Category { get; set; }
    }
}