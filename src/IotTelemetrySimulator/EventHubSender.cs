using Microsoft.Azure.EventHubs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IotTelemetrySimulator
{
    public class EventHubSender : SenderBase<EventData>
    {
        private EventHubClient eventHubClient;

        public EventHubSender(EventHubClient eventHubClient, string deviceId, RunnerConfiguration config) : base(deviceId, config)
        {
            this.eventHubClient = eventHubClient;
        }

        protected override async Task SendAsync(EventData msg, CancellationToken cancellationToken)
        {
            await eventHubClient.SendAsync(msg);
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
