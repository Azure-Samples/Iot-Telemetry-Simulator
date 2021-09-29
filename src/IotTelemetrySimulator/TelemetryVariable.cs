namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class TelemetryVariable
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("random")]
        public bool Random { get; set; }

        [JsonProperty("sequence")]
        public bool Sequence { get; set; }

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

        IReadOnlyList<string> referencedVariableNames;

        public IReadOnlyList<string> GetReferenceVariableNames()
        {
            if (this.referencedVariableNames == null)
            {
                if (this.Values == null || this.Values.Length == 0)
                {
                    this.referencedVariableNames = Array.Empty<string>();
                }
                else
                {
                    var variableNames = new List<string>();
                    foreach (var val in this.Values)
                    {
                        if (val is string valString && valString.StartsWith("$."))
                        {
                            variableNames.Add(valString[2..]);
                        }
                    }

                    this.referencedVariableNames = variableNames.ToArray();
                }
            }

            return this.referencedVariableNames;
        }
    }
}
