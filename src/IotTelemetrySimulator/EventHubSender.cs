namespace IotTelemetrySimulator
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;

    public class EventHubSender : SenderBase<EventData>
    {
        private readonly EventHubClient eventHubClient;

        public EventHubSender(EventHubClient eventHubClient, string deviceId, RunnerConfiguration config)
            : base(deviceId, config)
        {
            this.eventHubClient = eventHubClient;
        }

        protected override async Task SendAsync(EventData msg, CancellationToken cancellationToken)
        {
            string partitionKey = this.FillTelemetryTemplate(this.Config.PartitionKey);

            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                await this.eventHubClient.SendAsync(msg);
            }
            else
            {
                await this.eventHubClient.SendAsync(msg, partitionKey);
            }
        }

        public override Task OpenAsync()
        {
            return Task.CompletedTask;
        }

        protected override EventData BuildMessage(byte[] messageBytes)
        {
            return new EventData(messageBytes);
        }

        protected override void SetMessageProperty(EventData msg, string key, string value)
        {
            msg.Properties[key] = value;
        }

        protected override bool IsTransientException(Exception exception)
        {
            return exception is EventHubsCommunicationException;
        }
    }
}
