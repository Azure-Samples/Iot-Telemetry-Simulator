namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;

    public abstract class PayloadBase
    {
        public int Distribution { get; set; }

        public PayloadBase(int distribution)
        {
            if (distribution < 1 || distribution > 100)
                throw new ArgumentOutOfRangeException(nameof(distribution), "Distribution must be between 1 and 100");

            this.Distribution = distribution;
        }

        public abstract (byte[], Dictionary<string, object>) Generate(Dictionary<string, object> variableValues);

        public abstract string GetDescription();
    }
}
