using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
            variableValues = config.Variables.NextValues(variableValues);
            variableValues[Constants.DeviceIdValueName] = deviceId;

            var data = config.Template.Create(variableValues);
            var messageBytes = Encoding.UTF8.GetBytes(data);
            var msg = new Message(messageBytes)
            {
                ContentEncoding = Utf8Encoding,
                ContentType = ApplicationJsonContentType,
            };


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


            return msg;
        }
    }
}
