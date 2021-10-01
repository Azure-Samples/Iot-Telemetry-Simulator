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
        private readonly List<int> interval;
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
            int counter = 0;
            try
            {
                await this.sender.OpenAsync();
                stats.IncrementDeviceConnected();

                // Delay first event by a random amount to avoid bursts
                int crtInterval = GetCurrentInterval(0, this.interval, counter);

                await Task.Delay(this.random.Next(crtInterval), cancellationToken);

                var stopwatch = new Stopwatch();
                stopwatch.Start();
                for (var i = 0L; !cancellationToken.IsCancellationRequested && (this.config.MessageCount <= 0 || i < this.config.MessageCount); i++)
                {
                    await this.sender.SendMessageAsync(stats, cancellationToken);
                    if (counter >= this.interval.Count)
                    {
                        counter = 0;
                    }

                    crtInterval = GetCurrentInterval(crtInterval, this.interval, counter);
                    counter++;
                    Console.WriteLine("Sending total" + crtInterval);
                    var millisecondsDelay = Math.Max(0, crtInterval - stopwatch.ElapsedMilliseconds);
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

            // Returns the interval sum to the counter point
            int GetCurrentInterval(int previousSum, List<int> intervals, int counter)
            {
                return previousSum + intervals[counter];
            }
        }
    }
}
