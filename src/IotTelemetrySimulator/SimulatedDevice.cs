using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IotTelemetrySimulator
{
    public class SimulatedDevice
    {
        const string ApplicationJsonContentType = "application/json";
        const string Utf8Encoding = "utf8";
        const int WaitTimeOnIotHubError = 5_000;
        const string ActivityIdPropertyName = "Diagnostic-Id";



        private string deviceId;
        private RunnerConfiguration config;
        private DeviceClient deviceClient;
        private Dictionary<string, object> variableValues;

        public string DeviceID => deviceId;

        public SimulatedDevice(string deviceId, RunnerConfiguration config, DeviceClient deviceClient)
        {
            this.deviceId = deviceId;
            this.config = config;
            this.deviceClient = deviceClient;
        }

        public Task Start(RunnerStats stats, CancellationToken cancellationToken)
        {
            return Task.Run(() => RunnerAsync(stats, cancellationToken), cancellationToken);
        }

        async Task RunnerAsync(RunnerStats stats, CancellationToken cancellationToken)
        {
            try
            {
                await deviceClient.OpenAsync();
                stats.IncrementDeviceConnected();

                for (var i=0; !cancellationToken.IsCancellationRequested && (config.MessageCount <= 0 || i < config.MessageCount); i++)
                {
                    await Task.Delay(config.Interval, cancellationToken);
                    await SendMessageAsync(stats, cancellationToken);
                }

                stats.IncrementCompletedDevice();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        private async Task SendMessageAsync(RunnerStats stats, CancellationToken cancellationToken)
        {
            const int MaxAttempts = 3;
            var msg = CreateMessage();

            for (var attempt = 1; attempt <= MaxAttempts; ++attempt)
            {
                try
                {
                    await deviceClient.SendEventAsync(msg, cancellationToken);
                    stats.IncrementMessageSent();
                    break;
                }
                catch (IotHubCommunicationException)
                {
                    stats.IncrementSendTelemetryErrors();
                    await Task.Yield();
                }
                catch (IotHubException)
                {
                    stats.IncrementSendTelemetryErrors();
                    await Task.Delay(WaitTimeOnIotHubError);
                }
            }
        }

        public virtual Message CreateMessage()
        {
            if (variableValues == null)
            {
                variableValues = new Dictionary<string, object> 
                {
                    { Constants.DeviceIdValueName, DeviceID }
                };
            }

            var (messageBytes, nextVariableValues) = config.PayloadGenerator.Generate(variableValues);
            variableValues = nextVariableValues;
     
            var msg = new Message(messageBytes)
            {
                CorrelationId = Guid.NewGuid().ToString(),
            };

            msg.ContentEncoding = Utf8Encoding;
            msg.ContentType = ApplicationJsonContentType;

            if (config.Header != null)
            {
                var headerJson = config.Header.Create(variableValues);
                if (!string.IsNullOrWhiteSpace(headerJson))
                {
                    var headerValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(headerJson);
                    foreach (var kv in headerValues)
                    {
                        if (kv.Value != null)
                        {
                            msg.Properties[kv.Key] = kv.Value;
                        }
                    }
                }
            }

            // https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-distributed-tracing#workaround-for-third-party-clients
            // https://github.com/Azure/azure-event-hubs-dotnet/blob/8aae6b6ec1af44de69326288854f5811985db539/src/Microsoft.Azure.EventHubs/EventHubsDiagnosticSource.cs
            //msg.Properties[ActivityIdPropertyName] = ActivitySpanId.CreateRandom().ToHexString();

            return msg;
        }
    }
}
