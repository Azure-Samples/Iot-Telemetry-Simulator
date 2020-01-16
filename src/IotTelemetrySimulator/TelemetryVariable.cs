using Newtonsoft.Json;

namespace IotTelemetrySimulator
{
    public class TelemetryVariable
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("random")]
        public bool Random { get; set; }

        [JsonProperty("max")]
        public int? Max { get; set; }

        [JsonProperty("min")]
        public int? Min { get; set; }

        [JsonProperty("step")]
        public int? Step { get; set; }

        [JsonProperty("customLengthString")]
        public int? CustomLengthString { get; set; }

        [JsonProperty("values")]
        public object[] Values { get; set; }
    }
}
