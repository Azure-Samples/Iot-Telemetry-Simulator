using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace IotTelemetrySimulator
{
    public class RunnerConfiguration
    {
        public string IotHubConnectionString { get; set; }
        public string DevicePrefix { get; set; } = "sim";
        public int DeviceIndex { get; set; } = 1;
        public int DeviceCount { get; set; } = 1;
        public IReadOnlyList<string> DeviceList { get; set; }
        public int MessageCount { get; set; } = 10;
        public int Interval { get; set; } = 1_000;

        public TelemetryTemplate Template { get; set; }

        public TelemetryTemplate Header { get; set; }

        public TelemetryValues Variables { get; set; }

        public byte[] FixPayload { get; set; }

        public void EnsureIsValid()
        {
            if (string.IsNullOrEmpty(IotHubConnectionString))
                throw new Exception($"{nameof(IotHubConnectionString)} was not defined");

            if (Interval <= 0)
                throw new Exception($"{nameof(Interval)} must be greater than zero");
        }


        internal static RunnerConfiguration Load(IConfiguration configuration, ILogger logger)
        {
            var config = new RunnerConfiguration();
            config.IotHubConnectionString = configuration.GetValue<string>(nameof(IotHubConnectionString));
            config.DevicePrefix = configuration.GetValue(nameof(DevicePrefix), config.DevicePrefix);
            config.DeviceIndex = configuration.GetValue(nameof(DeviceIndex), config.DeviceIndex);
            config.DeviceCount = configuration.GetValue(nameof(DeviceCount), config.DeviceCount);
            config.MessageCount = configuration.GetValue(nameof(MessageCount), config.MessageCount);
            config.Interval = configuration.GetValue(nameof(Interval), config.Interval);

            var rawFixTelemetry = configuration.GetValue<string>(nameof(FixPayload));
            if (rawFixTelemetry != null)
            {
                config.FixPayload = Convert.FromBase64String(rawFixTelemetry);
                logger.LogWarning("Using fix payload telemetry");
            }
            else if (int.TryParse(configuration.GetValue<string>("FixPayloadSize"), out var fixPayloadSize) && fixPayloadSize > 0)
            {
                config.FixPayload = new byte[fixPayloadSize];
                logger.LogWarning("Using fix payload telemetry with size {size}", fixPayloadSize);
            }
            else
            {
                var rawTelemetryTemplate = configuration.GetValue<string>(nameof(Template));
                if (!string.IsNullOrWhiteSpace(rawTelemetryTemplate))
                {
                    config.Template = new TelemetryTemplate(rawTelemetryTemplate);
                }
                else
                {
                    logger.LogWarning("Using default telemetry template");
                    config.Template = new TelemetryTemplate();
                }
            }

            var rawDeviceList = configuration.GetValue<string>(nameof(DeviceList));
            if (!string.IsNullOrWhiteSpace(rawDeviceList))
            {
                config.DeviceList = rawDeviceList.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (config.DeviceList.Count > 0)
                {
                    config.DeviceCount = config.DeviceList.Count;
                    config.DevicePrefix = string.Empty;
                }
            }            

            var rawHeaderTemplate = configuration.GetValue<string>(nameof(Header));
            if (!string.IsNullOrWhiteSpace(rawHeaderTemplate))
            {
                config.Header = new TelemetryTemplate(rawHeaderTemplate);
            }

            var rawValues = configuration.GetValue<string>(nameof(Variables));
            if (!string.IsNullOrWhiteSpace(rawValues))
            {
                try
                {
                    var values = JsonConvert.DeserializeObject<TelemetryVariable[]>(rawValues);
                    config.Variables = new TelemetryValues(values);
                }
                catch (JsonReaderException ex)
                {
                    throw new Exception($"Failed to parse variables from: {rawValues}", ex);
                }
            }
            else
            {
                logger.LogWarning("No custom telemetry variables found");
                config.Variables = new TelemetryValues(new TelemetryVariable[] {
                    new TelemetryVariable
                    {
                        Min = 1,
                        Name = "Counter",
                        Step = 1,
                    }
                });
            }

            return config;
        }
    }
}
