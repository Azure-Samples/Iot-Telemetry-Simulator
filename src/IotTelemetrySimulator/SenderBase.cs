using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IotTelemetrySimulator
{
    public abstract class SenderBase<TMessage> : ISender
    {
        protected const int WaitTimeOnTransientError = 5_000;
        protected const int MaxSendAttempts = 3;

        protected string deviceId;
        protected RunnerConfiguration config;
        protected Dictionary<string, object> variableValues;

        public SenderBase(string deviceId, RunnerConfiguration config)
        {
            this.deviceId = deviceId;
            this.config = config;
        }

        public abstract Task OpenAsync();

        public async Task SendMessageAsync(RunnerStats stats, CancellationToken cancellationToken)
        {
            var msg = CreateMessage();

            for (var attempt = 1; attempt <= MaxSendAttempts; ++attempt)
            {
                try
                {
                    await SendAsync(msg, cancellationToken);
                    stats.IncrementMessageSent();
                    break;
                }
                catch (Exception ex) when (IsTransientException(ex))
                {
                    stats.IncrementSendTelemetryErrors();
                    await Task.Yield();
                }
                catch (Exception)
                {
                    stats.IncrementSendTelemetryErrors();
                    await Task.Delay(WaitTimeOnTransientError);
                }
            }
        }

        protected abstract Task SendAsync(TMessage msg, CancellationToken cancellationToken);

        protected TMessage CreateMessage()
        {
            if (variableValues == null)
            {
                variableValues = new Dictionary<string, object>
                {
                    { Constants.DeviceIdValueName, deviceId }
                };
            }

            var (messageBytes, nextVariableValues) = config.PayloadGenerator.Generate(variableValues);
            variableValues = nextVariableValues;

            TMessage msg = BuildMessage(messageBytes);

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
                            SetMessageProperty(msg, kv.Key, kv.Value);
                        }
                    }
                }
            }

            return msg;
        }

        protected abstract void SetMessageProperty(TMessage msg, string key, string value);

        protected abstract TMessage BuildMessage(byte[] messageBytes);

        protected abstract bool IsTransientException(Exception exception);
    }
}
