using System;
using System.Collections.Generic;
using System.Text;

namespace IotTelemetrySimulator
{
    public class TemplatedPayload : PayloadBase
    {
        public TelemetryTemplate Template { get; }
        public TelemetryValues Variables { get; }

        public TemplatedPayload(int distribution, TelemetryTemplate template, TelemetryValues variables) : base(distribution)
        {
            Template = template;
            Variables = variables;
        }

        public override (byte[], Dictionary<string, object>) Generate(Dictionary<string, object> variableValues)
        {
            var nextVariables = Variables.NextValues(variableValues);
            var data = Template.Create(nextVariables);
            return (Encoding.UTF8.GetBytes(data), nextVariables);
        }

        public override string GetDescription()
        {
            return $"Template: {Template.ToString()}";
        }
    }
}
