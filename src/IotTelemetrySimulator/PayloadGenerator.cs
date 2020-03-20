using System;
using System.Collections.Generic;
using System.Linq;

namespace IotTelemetrySimulator
{
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

            Payloads = payloads.OrderByDescending(x => x.Distribution).ToArray();            
        }

        public (byte[], Dictionary<string, object>) Generate(Dictionary<string, object> variableValues)
        {
            if (Payloads.Length == 1)
                return Payloads[0].Generate(variableValues);

            var random = randomizer.GetNext(1, 101);
            var currentPercentage = 0;
            foreach (var payload in Payloads)
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
            return string.Join(',', Payloads.Select(x => x.GetDescription()));
        }
    }
}
