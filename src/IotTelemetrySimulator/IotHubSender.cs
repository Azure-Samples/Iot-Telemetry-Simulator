using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Exceptions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IotTelemetrySimulator
{
    internal class IotHubSender : SenderBase<Message>
    {
        const string ApplicationJsonContentType = "application/json";
        const string Utf8Encoding = "utf8";

        private DeviceClient deviceClient;

        public IotHubSender(DeviceClient deviceClient, string deviceId, RunnerConfiguration config) : base(deviceId, config)
        {
            this.deviceClient = deviceClient;
        }

        public override async Task OpenAsync()
        {
            await deviceClient.OpenAsync();
        }

        protected override async Task SendAsync(Message msg, CancellationToken cancellationToken)
        {
            await deviceClient.SendEventAsync(msg, cancellationToken);
        }

        protected override Message BuildMessage(byte[] messageBytes)
        {
            var msg = new Message(messageBytes)
            {
                CorrelationId = Guid.NewGuid().ToString(),
            };

            msg.ContentEncoding = Utf8Encoding;
            msg.ContentType = ApplicationJsonContentType;

            return msg;
        }

        protected override void SetMessageProperty(Message msg, string key, string value)
        {
            msg.Properties[key] = value;
        }

        protected override bool IsTransientException(Exception exception)
        {
            return exception is IotHubCommunicationException;
        }
    }
}
