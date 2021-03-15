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

        private readonly IRandomizer random = new DefaultRandomizer();
        private readonly string deviceId;
        private Dictionary<string, object> variableValues;

        protected RunnerConfiguration Config { get; }

        protected SenderBase(string deviceId, RunnerConfiguration config)
        {
            this.deviceId = deviceId;
            this.Config = config;
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
                    if (this.Config.DuplicateEvery <= 0
                        || this.random.Next(this.Config.DuplicateEvery) != 0)
                        break;
                    attempt = 1;
                }
                catch (Exception ex) when (this.IsTransientException(ex))
                {
                    stats.IncrementSendTelemetryErrors();
                    await Task.Yield();
                }
                catch (Exception)
                {
                    stats.IncrementSendTelemetryErrors();
                    await Task.Delay(WaitTimeOnTransientError, cancellationToken);
                }
            }
        }

        protected abstract Task SendAsync(TMessage msg, CancellationToken cancellationToken);

        private TMessage CreateMessage()
        {
            this.variableValues ??= new Dictionary<string, object>
            {
                { Constants.DeviceIdValueName, this.deviceId },
            };

            var (messageBytes, nextVariableValues) = this.Config.PayloadGenerator.Generate(this.variableValues);
            this.variableValues = nextVariableValues;

            var msg = this.BuildMessage(messageBytes);

            var headerJson = this.FillTelemetryTemplate(this.Config.Header);
            if (!string.IsNullOrWhiteSpace(headerJson))
            {
                var headerValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(headerJson);
                foreach (var (key, value) in headerValues)
                {
                    if (value != null)
                    {
                        this.SetMessageProperty(msg, key, value);
                    }
                }
            }

            return msg;
        }

        protected string FillTelemetryTemplate(TelemetryTemplate telemetryTemplate)
        {
            return telemetryTemplate?.Create(this.variableValues);
        }

        protected abstract void SetMessageProperty(TMessage msg, string key, string value);

        protected abstract TMessage BuildMessage(byte[] messageBytes);

        protected abstract bool IsTransientException(Exception exception);
    }
}
