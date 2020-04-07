namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class PayloadGenerator
    {
        private readonly IRandomizer randomizer;

        public PayloadBase[] Payloads { get; }

        public PayloadGenerator(IEnumerable<PayloadBase> payloads, IRandomizer randomizer)
        {
            this.randomizer = randomizer ?? throw new ArgumentNullException(nameof(randomizer));
            if (payloads is null)
            {
                throw new ArgumentNullException(nameof(payloads));
            }

            this.Payloads = payloads.OrderByDescending(x => x.Distribution).ToArray();
        }

        public (byte[], Dictionary<string, object>) Generate(Dictionary<string, object> variableValues)
        {
            if (this.Payloads.Length == 1)
                return this.Payloads[0].Generate(variableValues);

            var random = this.randomizer.GetNext(1, 101);
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
