namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class PayloadGenerator
    {
        private readonly IRandomizer randomizer;

        public PayloadBase[] Payloads { get; }

        private readonly Dictionary<string, PayloadBase> payloadsPerDevice;

        public PayloadGenerator(IEnumerable<PayloadBase> payloads, IRandomizer randomizer)
        {
            this.randomizer = randomizer ?? throw new ArgumentNullException(nameof(randomizer));
            if (payloads is null)
            {
                throw new ArgumentNullException(nameof(payloads));
            }

            this.Payloads = payloads.OrderByDescending(x => x.Distribution).ToArray();
            this.payloadsPerDevice = new Dictionary<string, PayloadBase>();
            foreach (var payload in this.Payloads)
            {
                if (!string.IsNullOrEmpty(payload.DeviceId))
                {
                    this.payloadsPerDevice[payload.DeviceId] = payload;
                }
            }
        }

        public (byte[], Dictionary<string, object>) Generate(string deviceId, Dictionary<string, object> variableValues)
        {
            if (this.Payloads.Length == 1)
                return this.Payloads[0].Generate(variableValues);

            if (!string.IsNullOrEmpty(deviceId) && this.payloadsPerDevice.TryGetValue(deviceId, out var payloadForDevice))
                return payloadForDevice.Generate(variableValues);

            var random = this.randomizer.Next(1, 101);
            var currentPercentage = 0;
            foreach (var payload in this.Payloads)
            {
                var currentThreshold = currentPercentage + payload.Distribution;
                if (currentThreshold >= random)
                    return payload.Generate(variableValues);

                currentPercentage = currentThreshold;
            }

            throw new Exception("Invalid payload distribution");
        }

        public string GetDescription()
        {
            return string.Join(',', this.Payloads.Select(x => x.GetDescription()));
        }
    }
}
