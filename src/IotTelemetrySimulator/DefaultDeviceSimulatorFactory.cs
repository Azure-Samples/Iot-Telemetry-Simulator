﻿namespace IotTelemetrySimulator
{
    using System;
    using System.Linq;
    using Confluent.Kafka;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.EventHubs;

    public class DefaultDeviceSimulatorFactory : IDeviceSimulatorFactory
    {
        private EventHubClient eventHubClient;
        private IProducer<Null, byte[]> kafkaProducer;

        public SimulatedDevice Create(string deviceId, RunnerConfiguration config)
        {
            var sender = this.GetSender(deviceId, config);
            return new SimulatedDevice(deviceId, config, sender);
        }

        private ISender GetSender(string deviceId, RunnerConfiguration config)
        {
            if (!string.IsNullOrEmpty(config.IotHubConnectionString))
            {
                return GetIotHubSender(deviceId, config);
            }

            if (!string.IsNullOrEmpty(config.EventHubConnectionString))
            {
                return this.CreateEventHubSender(deviceId, config);
            }

            if (config.KafkaConnectionProperties != null)
            {
                return this.CreateKafkaSender(deviceId, config);
            }

            throw new ArgumentException("No connection string specified");
        }

        private static ISender GetIotHubSender(string deviceId, RunnerConfiguration config)
        {
            // create one deviceClient for each device
            var deviceClient = DeviceClient.CreateFromConnectionString(
                config.IotHubConnectionString,
                deviceId,
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(Microsoft.Azure.Devices.Client.TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                        },
                    },
                });

            return new IotHubSender(deviceClient, deviceId, config);
        }

        private ISender CreateEventHubSender(string deviceId, RunnerConfiguration config)
        {
            // Reuse the same eventHubClient for all devices
            this.eventHubClient ??= EventHubClient.CreateFromConnectionString(config.EventHubConnectionString);
            return new EventHubSender(this.eventHubClient, deviceId, config);
        }

        private ISender CreateKafkaSender(string deviceId, RunnerConfiguration config)
        {
            // Reuse the same KafkaProducer for all devices
            this.kafkaProducer ??= new ProducerBuilder<Null, byte[]>(config.KafkaConnectionProperties.ToList()).Build();
            return new KafkaSender(this.kafkaProducer, deviceId, config, config.KafkaTopic);
        }
    }
}