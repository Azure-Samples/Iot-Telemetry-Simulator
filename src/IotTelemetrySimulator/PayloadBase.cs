namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;

    public abstract class PayloadBase
    {
        public int Distribution { get; set; }

        public string DeviceId { get; set; }

        protected PayloadBase(int distribution)
            : this(distribution, null)
        {
        }

        protected PayloadBase(int distribution, string deviceId)
        {
            if (distribution < 1 || distribution > 100)
                throw new ArgumentOutOfRangeException(nameof(distribution), "Distribution must be between 1 and 100");

            this.Distribution = distribution;
            this.DeviceId = deviceId;
        }

        public abstract (byte[], Dictionary<string, object>) Generate(Dictionary<string, object> variableValues);

        public abstract string GetDescription();
    }
}
