namespace IotTelemetrySimulator
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class SimulatedDevice
    {
        private readonly ISender sender;
        private RunnerConfiguration config;

        public string DeviceID { get; private set; }

        public SimulatedDevice(string deviceId, RunnerConfiguration config, ISender sender)
        {
            this.DeviceID = deviceId;
            this.config = config;
            this.sender = sender;
        }

        public Task Start(RunnerStats stats, CancellationToken cancellationToken)
        {
            return Task.Run(() => this.RunnerAsync(stats, cancellationToken), cancellationToken);
        }

        async Task RunnerAsync(RunnerStats stats, CancellationToken cancellationToken)
        {
            try
            {
                await this.sender.OpenAsync();
                stats.IncrementDeviceConnected();

                for (var i = 0; !cancellationToken.IsCancellationRequested && (this.config.MessageCount <= 0 || i < this.config.MessageCount); i++)
                {
                    await Task.Delay(this.config.Interval, cancellationToken);
                    await this.sender.SendMessageAsync(stats, cancellationToken);
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
