namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public abstract class SenderBase<TMessage> : ISender
    {
        protected const int WaitTimeOnTransientError = 5_000;
        protected const int MaxSendAttempts = 3;

        private string deviceId;
        private RunnerConfiguration config;
        private Dictionary<string, object> variableValues;

        public SenderBase(string deviceId, RunnerConfiguration config)
        {
            this.deviceId = deviceId;
            this.config = config;
        }

        public abstract Task OpenAsync();

        public async Task SendMessageAsync(RunnerStats stats, CancellationToken cancellationToken)
        {
            var msg = this.CreateMessage();

            for (var attempt = 1; attempt <= MaxSendAttempts; ++attempt)
            {
                try
                {
                    await this.SendAsync(msg, cancellationToken);
                    stats.IncrementMessageSent();
                    break;
                }
                catch (Exception ex) when (this.IsTransientException(ex))
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
            if (this.variableValues == null)
            {
                this.variableValues = new Dictionary<string, object>
                {
                    { Constants.DeviceIdValueName, this.deviceId }
                };
            }

            var (messageBytes, nextVariableValues) = this.config.PayloadGenerator.Generate(this.variableValues);
            this.variableValues = nextVariableValues;

            TMessage msg = this.BuildMessage(messageBytes);

            if (this.config.Header != null)
            {
                var headerJson = this.config.Header.Create(this.variableValues);
                if (!string.IsNullOrWhiteSpace(headerJson))
                {
                    var headerValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(headerJson);
                    foreach (var kv in headerValues)
                    {
                        if (kv.Value != null)
                        {
                            this.SetMessageProperty(msg, kv.Key, kv.Value);
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
