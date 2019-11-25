using Microsoft.Azure.Devices.Client;

namespace IotTelemetrySimulator
{
    public class DefaultDeviceSimulatorFactory : IDeviceSimulatorFactory
    {
        public SimulatedDevice Create(string deviceId, RunnerConfiguration config)
        {
            var deviceClient = DeviceClient.CreateFromConnectionString(
                config.IotHubConnectionString,
                deviceId,
                new ITransportSettings[]
                        {
                            new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                            {
                                AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                                {
                                    Pooling = true,
                                }
                            }
                        });

            return new SimulatedDevice(deviceId, config, deviceClient);
        }

    }
}