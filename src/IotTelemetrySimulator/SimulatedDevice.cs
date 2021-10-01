namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public class SimulatedDevice
    {
        private readonly ISender sender;
        private readonly int[] interval;
        private readonly RunnerConfiguration config;
        private readonly IRandomizer random = new DefaultRandomizer();

        public string DeviceID { get; private set; }

        public SimulatedDevice(string deviceId, RunnerConfiguration config, ISender sender)
        {
            this.DeviceID = deviceId;
            this.config = config;
            this.sender = sender;
            this.interval = config.GetMessageIntervalForDevice(deviceId);
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

                // Delay first event by a random amount to avoid bursts
                int crtInterval = this.interval[0];

                await Task.Delay(this.random.Next(crtInterval), cancellationToken);

                var stopwatch = new Stopwatch();
                stopwatch.Start();
                ulong totalIntervalTime = 0;
                for (var i = 0L; !cancellationToken.IsCancellationRequested && (this.config.MessageCount <= 0 || i < this.config.MessageCount); i++)
                {
                    await this.sender.SendMessageAsync(stats, cancellationToken);

                    crtInterval = this.interval[i % this.interval.Length];
                    totalIntervalTime = totalIntervalTime + (ulong)crtInterval;

                    var millisecondsDelay = Math.Max(0, totalIntervalTime - (ulong)stopwatch.ElapsedMilliseconds);
                    await Task.Delay((int)millisecondsDelay, cancellationToken);
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
