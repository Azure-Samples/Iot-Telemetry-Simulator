namespace IotTelemetrySimulator
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Confluent.Kafka;

    public class KafkaSender : SenderBase<Message<Null, byte[]>>
    {
        private readonly IProducer<Null, byte[]> producer;
        private readonly string topic;

        public KafkaSender(IProducer<Null, byte[]> producer, string deviceId, RunnerConfiguration config, string topic)
            : base(deviceId, config)
        {
            this.producer = producer;
            this.topic = topic;
        }

        protected override async Task SendAsync(Message<Null, byte[]> msg, CancellationToken cancellationToken)
        {
           await this.producer.ProduceAsync(this.topic, msg, cancellationToken);
        }

        public override Task OpenAsync()
        {
            return Task.CompletedTask;
        }

        protected override Message<Null, byte[]> BuildMessage(byte[] messageBytes)
        {
            return new Message<Null, byte[]> { Value = messageBytes, Headers = new Headers() };
        }

        protected override void SetMessageProperty(Message<Null, byte[]> msg, string key, string value)
        {
            msg.Headers.Add(key, Encoding.UTF8.GetBytes(value));
        }

        protected override bool IsTransientException(Exception exception)
        {
            return false;
        }
    }
}
