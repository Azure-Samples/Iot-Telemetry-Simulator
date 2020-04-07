namespace IotTelemetrySimulator
{
    using System.Collections.Generic;

    public class FixPayload : PayloadBase
    {
        public byte[] Payload { get; }

        public FixPayload(int distribution, byte[] payload)
            : base(distribution)
        {
            this.Payload = payload;
        }

        public override (byte[], Dictionary<string, object>) Generate(Dictionary<string, object> variableValues)
        {
            return (this.Payload, variableValues);
        }

        public override string GetDescription()
        {
            return $"Fix: {this.Payload.Length} bytes";
        }
    }
}
