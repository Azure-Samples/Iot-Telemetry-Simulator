namespace IotTelemetrySimulator
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.EventHubs;
    using Azure.Messaging.EventHubs.Producer;

    public class EventHubSender : SenderBase<EventData>
    {
        private readonly EventHubProducerClient eventHubProducer;

        public EventHubSender(EventHubProducerClient eventHubProducer, string deviceId, RunnerConfiguration config)
            : base(deviceId, config)
        {
            this.eventHubProducer = eventHubProducer;
        }

        protected override async Task SendAsync(EventData msg, CancellationToken cancellationToken)
        {
            string partitionKey = this.FillTelemetryTemplate(this.Config.PartitionKey);

            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                await this.eventHubProducer.SendAsync(new EventData[] { msg });
            }
            else
            {
                await this.eventHubProducer.SendAsync(new EventData[] { msg }, new SendEventOptions { PartitionKey = partitionKey });
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
            return exception is EventHubsException eventHubsException && eventHubsException.IsTransient;
        }
    }
}
