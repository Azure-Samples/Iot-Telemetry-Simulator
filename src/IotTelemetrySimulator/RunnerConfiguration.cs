namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class RunnerConfiguration
    {
        public const string DefaultTemplate = "{\"deviceId\": \"$.DeviceId\", \"time\": \"$.Time\", \"counter\": $.Counter}";
        private const string RegexExpression = "(?<type>fixsize|template|fix)(\\()(?<pv>[[0-9a-z,=,\\s]+)";
        static readonly Regex TemplateParser = new Regex(RegexExpression, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public string IotHubConnectionString { get; set; }

        public string EventHubConnectionString { get; set; }

        public IDictionary<string, string> KafkaConnectionProperties { get; set; }

        public string KafkaTopic { get; set; }

        public string DevicePrefix { get; set; } = "sim";

        public int DeviceIndex { get; set; } = 1;

        public int DeviceCount { get; set; } = 1;

        public IReadOnlyList<string> DeviceList { get; set; }

        public int MessageCount { get; set; } = 10;

        public int Interval { get; set; } = 1_000;

        public int DuplicateEvery { get; private set; }

        public PayloadGenerator PayloadGenerator { get; private set; }

        public TelemetryTemplate Header { get; set; }

        public TelemetryTemplate PartitionKey { get; set; }

        public TelemetryValues Variables { get; set; }

        public byte[] FixPayload { get; set; }

        public Dictionary<string, int> Intervals { get; set; }

        public void EnsureIsValid()
        {
            var numberOfConnectionSettings = 0;
            if (!string.IsNullOrWhiteSpace(this.IotHubConnectionString))
                numberOfConnectionSettings++;
            if (!string.IsNullOrWhiteSpace(this.EventHubConnectionString))
                numberOfConnectionSettings++;
            if (this.KafkaConnectionProperties != null)
                numberOfConnectionSettings++;
            if (numberOfConnectionSettings != 1)
            {
                throw new Exception(
                    $"Exactly one of {nameof(this.IotHubConnectionString)}, {nameof(this.EventHubConnectionString)} or {nameof(this.KafkaConnectionProperties)} must be defined");
            }

            if (this.KafkaConnectionProperties != null
                && string.IsNullOrWhiteSpace(this.KafkaTopic))
            {
                throw new Exception(
                    $"{nameof(this.KafkaTopic)} is required");
            }

            if (this.KafkaConnectionProperties == null
                && !string.IsNullOrWhiteSpace(this.KafkaTopic))
            {
                throw new Exception(
                    $"{nameof(this.KafkaConnectionProperties)} is required");
            }

            if (this.KafkaConnectionProperties != null
                && (!this.KafkaConnectionProperties.ContainsKey("bootstrap.servers")
                || string.IsNullOrWhiteSpace(this.KafkaConnectionProperties["bootstrap.servers"])))
            {
                throw new Exception(
                    $"{nameof(this.KafkaConnectionProperties)} should contain at least a value for bootstrap.servers");
            }

            if (this.Interval <= 0)
                throw new Exception($"{nameof(this.Interval)} must be greater than zero");

            if (this.DuplicateEvery < 0)
                throw new Exception($"{nameof(this.DuplicateEvery)} must be greater than or equal to zero");
        }

        public int GetMessageIntervalForDevice(string deviceId)
        {
            if (this.Intervals != null && this.Intervals.TryGetValue(deviceId, out var customInterval))
            {
                return customInterval;
            }

            return this.Interval;
        }

        public static RunnerConfiguration Load(IConfiguration configuration, ILogger logger)
        {
            var config = new RunnerConfiguration();
            config.IotHubConnectionString = configuration.GetValue<string>(nameof(IotHubConnectionString));
            config.EventHubConnectionString = configuration.GetValue<string>(nameof(EventHubConnectionString));
            config.DevicePrefix = configuration.GetValue(nameof(DevicePrefix), config.DevicePrefix);
            config.DeviceIndex = configuration.GetValue(nameof(DeviceIndex), config.DeviceIndex);
            config.DeviceCount = configuration.GetValue(nameof(DeviceCount), config.DeviceCount);
            config.MessageCount = configuration.GetValue(nameof(MessageCount), config.MessageCount);
            config.Interval = configuration.GetValue(nameof(Interval), config.Interval);
            config.DuplicateEvery = configuration.GetValue(nameof(DuplicateEvery), config.DuplicateEvery);

            var kafkaProperties = configuration.GetValue<string>(nameof(KafkaConnectionProperties));
            if (!string.IsNullOrWhiteSpace(kafkaProperties))
            {
                try
                {
                    var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(kafkaProperties);
                    config.KafkaConnectionProperties = values;
                }
                catch (JsonReaderException ex)
                {
                    throw new Exception($"Failed to parse properties from: {kafkaProperties}", ex);
                }
            }

            config.KafkaTopic = configuration.GetValue<string>(nameof(KafkaTopic));

            var variablesSection = configuration.GetSection(nameof(Variables));
            if (variablesSection.Exists() && variablesSection.Value is not string)
            {
                var values = new List<TelemetryVariable>();
                variablesSection.Bind(values);
                config.Variables = new TelemetryValues(values);
            }
            else
            {
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
                    config.Variables = new TelemetryValues(new[]
                    {
                        new TelemetryVariable
                        {
                            Min = 1,
                            Name = "Counter",
                            Step = 1,
                        }
                    });
                }
            }

            var futureVariableNames = config.Variables.VariableNames().ToList();

            config.PayloadGenerator = new PayloadGenerator(
                LoadPayloads(configuration, config, logger, futureVariableNames),
                new DefaultRandomizer());

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

            config.Intervals = LoadIntervals(configuration);

            config.Header = GetTelemetryTemplate(configuration, nameof(Header), futureVariableNames);
            config.PartitionKey = GetTelemetryTemplate(configuration, nameof(PartitionKey), futureVariableNames);

            return config;
        }

        private static Dictionary<string, int> LoadIntervals(IConfiguration configuration)
        {
            var section = configuration.GetSection(nameof(RunnerConfiguration.Intervals));
            if (!section.Exists())
            {
                return null;
            }

            var result = new Dictionary<string, int>();
            section.Bind(result);

            return result;
        }

        private static TelemetryTemplate GetTelemetryTemplate(IConfiguration configuration, string headerName, IEnumerable<string> futureVariableNames)
        {
            var rawHeaderTemplate = configuration.GetValue<string>(headerName);
            if (string.IsNullOrWhiteSpace(rawHeaderTemplate))
            {
                return null;
            }

            return new TelemetryTemplate(rawHeaderTemplate, futureVariableNames);
        }

        private static List<PayloadBase> LoadPayloads(IConfiguration configuration, RunnerConfiguration config, ILogger logger, ICollection<string> futureVariableNames)
        {
            List<PayloadBase> payloads = null;
            var payloadSection = configuration.GetSection("Payloads");
            if (payloadSection.Exists() && payloadSection.Value is not string)
            {
                payloads = LoadPayloadsFromSection(payloadSection, configuration, config, logger, futureVariableNames);
            }
            else
            {
                payloads = LoadPayloadsSimple(configuration, config, logger, futureVariableNames);
            }

            if (payloads.GroupBy(x => x.DeviceId).Any(g => g.Select(x => x.Distribution).Sum() != 100))
            {
                logger.LogWarning("Payload percentage distribution is not equal 100");
            }

            return payloads;
        }

        private static List<PayloadBase> LoadPayloadsSimple(IConfiguration configuration, RunnerConfiguration config, ILogger logger, ICollection<string> futureVariableNames)
        {
            var payloads = new List<PayloadBase>();

            var isDefaultTemplateContent = false;
            TelemetryTemplate defaultPayloadTemplate = null;
            var rawTelemetryTemplate = configuration.GetValue<string>(Constants.TemplateConfigName);
            if (!string.IsNullOrWhiteSpace(rawTelemetryTemplate))
            {
                defaultPayloadTemplate = new TelemetryTemplate(rawTelemetryTemplate, futureVariableNames);
            }
            else
            {
                defaultPayloadTemplate = new TelemetryTemplate(DefaultTemplate, futureVariableNames);
                isDefaultTemplateContent = true;
            }

            var rawDynamicPayload = configuration.GetValue<string>(Constants.PayloadDistributionConfigName);
            if (!string.IsNullOrEmpty(rawDynamicPayload))
            {
                var matches = TemplateParser.Matches(rawDynamicPayload);
                foreach (Match m in matches)
                {
                    if (m.Groups.TryGetValue("type", out var templateType) && m.Groups.TryGetValue("pv", out var paramValuesRaw))
                    {
                        var templateTypeLowercase = templateType.Value.ToLowerInvariant();

                        var paramValues = paramValuesRaw.Value.Split(",", StringSplitOptions.RemoveEmptyEntries);
                        if (paramValues.Length == 0)
                        {
                            logger.LogWarning("Expecting parameters in payload definition, found nothing template type '{value}'", templateTypeLowercase);
                            continue;
                        }

                        if (!int.TryParse(paramValues[0].Replace("%", string.Empty), out var distribution))
                        {
                            logger.LogWarning("Could not parse payload distribution from '{value}'", paramValues[0]);
                            continue;
                        }

                        switch (templateTypeLowercase)
                        {
                            case "fixsize":
                                if (paramValues.Length > 0 && int.TryParse(paramValues[1], out var fixSize) && fixSize >= 0)
                                {
                                    payloads.Add(new FixPayload(distribution, new byte[fixSize]));
                                }

                                break;

                            case "fix":
                                if (paramValues.Length > 0 && !string.IsNullOrWhiteSpace(paramValues[1]))
                                {
                                    var base64Text = paramValues[1].Trim();
                                    try
                                    {
                                        var bytes = Convert.FromBase64String(base64Text);
                                        payloads.Add(new FixPayload(distribution, bytes));
                                    }
                                    catch (Exception)
                                    {
                                        logger.LogWarning("Could not parse base64 payload '{value}'", base64Text);
                                    }
                                }

                                break;

                            case "template":
                                if (paramValues.Length == 1)
                                {
                                    payloads.Add(new TemplatedPayload(distribution, defaultPayloadTemplate, config.Variables));
                                }
                                else
                                {
                                    var templateName = paramValues[1].Trim();
                                    if (templateName.Equals("default", StringComparison.OrdinalIgnoreCase))
                                    {
                                        payloads.Add(new TemplatedPayload(distribution, defaultPayloadTemplate, config.Variables));
                                    }
                                    else
                                    {
                                        var rawTemplateValue = configuration.GetValue<string>(templateName);
                                        if (string.IsNullOrWhiteSpace(rawTemplateValue))
                                        {
                                            logger.LogWarning("Could not find template with name '{name}'", templateName);
                                        }
                                        else
                                        {
                                            payloads.Add(new TemplatedPayload(distribution, new TelemetryTemplate(rawTemplateValue, futureVariableNames), config.Variables));
                                        }
                                    }
                                }

                                break;

                            default:
                                logger.LogWarning("Unknown payload type '{type}'", templateTypeLowercase);
                                break;
                        }
                    }
                }
            }

            if (payloads.Count == 0)
            {
                payloads.Add(new TemplatedPayload(100, defaultPayloadTemplate, config.Variables));
                if (isDefaultTemplateContent)
                    logger.LogWarning("Using default telemetry template");
            }

            return payloads;
        }

        private static List<PayloadBase> LoadPayloadsFromSection(
            IConfigurationSection payloadSection,
            IConfiguration configuration,
            RunnerConfiguration config,
            ILogger logger,
            ICollection<string> futureVariableNames)
        {
            var payloads = new List<PayloadBase>();
            foreach (var subSection in payloadSection.GetChildren())
            {
                var deviceId = subSection["deviceId"];
                var distribution = subSection.GetValue("distribution", 100);
                PayloadBase payload = subSection["type"] switch
                {
                    "template" => new TemplatedPayload(distribution, deviceId, new TelemetryTemplate(GetTemplateFromSection(subSection, "template"), futureVariableNames), config.Variables),
                    "fix" => new FixPayload(distribution, deviceId, Encoding.UTF8.GetBytes(GetTemplateFromSection(subSection, "value"))),
                    _ => throw new InvalidOperationException("Unknown payload type. Accepted types are [template, fix]"),
                };

                payloads.Add(payload);
            }

            return payloads;
        }

        private static string GetTemplateFromSection(IConfigurationSection subSection, string key)
        {
            var templateConfigSection = subSection.GetSection(key);
            if (templateConfigSection.Value is string valueString)
            {
                return valueString;
            }

            var dictionaryVals = new Dictionary<string, string>();
            ConvertToDictionary(templateConfigSection, dictionaryVals, templateConfigSection);

            return JsonConvert.SerializeObject(dictionaryVals);
        }

        static void ConvertToDictionary(IConfiguration configuration, Dictionary<string, string> data = null, IConfigurationSection top = null)
        {
            if (data == null)
            {
                data = new Dictionary<string, string>();
            }

            var children = configuration.GetChildren();
            foreach (var child in children)
            {
                if (child.Value == null)
                {
                    ConvertToDictionary(configuration.GetSection(child.Key), data);
                    continue;
                }

                var key = top != null ? child.Path.Substring(top.Path.Length + 1) : child.Path;
                data[key] = child.Value;
            }
        }
    }
}
