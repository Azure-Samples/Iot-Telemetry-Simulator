using System.Collections.Generic;

namespace IotTelemetrySimulator
{
    public class FixPayload : PayloadBase
    {
        public byte[] Payload { get; }

        public FixPayload(int distribution, byte[] payload) : base(distribution)
        {
            Payload = payload;
        }

        public override (byte[], Dictionary<string, object>) Generate(Dictionary<string, object> variableValues)
        {
            return (Payload, variableValues);
        }

        public override string GetDescription()
        {
            return $"Fix: {Payload.Length} bytes";
        }
    }
}
