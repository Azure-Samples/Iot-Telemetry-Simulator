using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Text;

namespace IotTelemetrySimulator
{
    public class TelemetryTemplate
    {
        public const string DefaultTemplate = "{\"deviceId\": \"$.DeviceId\", \"time\": \"$.Time\", \"counter\": $.Counter}";

        private readonly string template;
        private readonly DefaultObjectPool<StringBuilder> stringBuilderPool;

        public TelemetryTemplate() : this(DefaultTemplate)
        {
        }

        public TelemetryTemplate(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("Invalid template", nameof(template));
            }

            this.template = template;
            stringBuilderPool = new DefaultObjectPool<StringBuilder>(new DefaultPooledObjectPolicy<StringBuilder>(), 100);
        }

        public string Create(Dictionary<string, object> values)
        {
            var builder = stringBuilderPool.Get();
            try
            {
                builder.Length = 0;
                builder.Append(template);

                if (values != null)
                {
                    foreach (var kv in values)
                    {
                        builder.Replace($"$.{kv.Key}", kv.Value.ToString());
                    }
                }

                return builder.ToString();
            }
            finally
            {
                stringBuilderPool.Return(builder);
            }
        }

        internal string GetTemplateDefinition()
        {
            return template;
        }

        public override string ToString()
        {
            return template;
        }
    }
}
