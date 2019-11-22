using Microsoft.Azure.Devices.Client;

namespace IotTelemetrySimulator
{
    public class DefaultDeviceSimulatorFactory : IDeviceSimulatorFactory
    {
        public SimulatedDevice Create(int deviceNumber, RunnerConfiguration config)
        {
            var deviceId = $"{config.DevicePrefix}{deviceNumber.ToString("000000")}";
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