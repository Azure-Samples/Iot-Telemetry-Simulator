using System;
using System.Threading;
using System.Threading.Tasks;

namespace IotTelemetrySimulator
{
    public class SimulatedDevice
    {        
        private RunnerConfiguration config;
        private readonly ISender sender;

        public string DeviceID { get; private set; }

        public SimulatedDevice(string deviceId, RunnerConfiguration config, ISender sender)
        {
            DeviceID = deviceId;
            this.config = config;
            this.sender = sender;
        }

        public Task Start(RunnerStats stats, CancellationToken cancellationToken)
        {
            return Task.Run(() => RunnerAsync(stats, cancellationToken), cancellationToken);
        }

        async Task RunnerAsync(RunnerStats stats, CancellationToken cancellationToken)
        {
            try
            {
                await sender.OpenAsync();
                stats.IncrementDeviceConnected();

                for (var i = 0; !cancellationToken.IsCancellationRequested && (config.MessageCount <= 0 || i < config.MessageCount); i++)
                {
                    await Task.Delay(config.Interval, cancellationToken);
                    await sender.SendMessageAsync(stats, cancellationToken);
                }

                stats.IncrementCompletedDevice();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }        
    }
}
