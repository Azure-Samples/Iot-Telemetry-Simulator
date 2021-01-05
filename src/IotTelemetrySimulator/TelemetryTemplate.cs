namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Extensions.ObjectPool;

    public class TelemetryTemplate
    {
        private readonly string template;
        private readonly DefaultObjectPool<StringBuilder> stringBuilderPool;

        public TelemetryTemplate(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("Invalid template", nameof(template));
            }

            this.template = template;
            this.stringBuilderPool = new DefaultObjectPool<StringBuilder>(new DefaultPooledObjectPolicy<StringBuilder>(), 100);
        }

        public string Create(Dictionary<string, object> values)
        {
            var builder = this.stringBuilderPool.Get();
            try
            {
                builder.Length = 0;
                builder.Append(this.template);

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
                this.stringBuilderPool.Return(builder);
            }
        }

        internal string GetTemplateDefinition()
        {
            return this.template;
        }

        public override string ToString()
        {
            return this.template;
        }
    }
}
