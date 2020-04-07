namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class TemplatedPayload : PayloadBase
    {
        public TelemetryTemplate Template { get; }

        public TelemetryValues Variables { get; }

        public TemplatedPayload(int distribution, TelemetryTemplate template, TelemetryValues variables)
            : base(distribution)
        {
            this.Template = template;
            this.Variables = variables;
        }

        public override (byte[], Dictionary<string, object>) Generate(Dictionary<string, object> variableValues)
        {
            var nextVariables = this.Variables.NextValues(variableValues);
            var data = this.Template.Create(nextVariables);
            return (Encoding.UTF8.GetBytes(data), nextVariables);
        }

        public override string GetDescription()
        {
            return $"Template: {this.Template.ToString()}";
        }
    }
}
