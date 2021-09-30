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
        private readonly object interval;
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
            int newcounter = 0;
            int crtInterval = 0;
            try
            {
                await this.sender.OpenAsync();
                stats.IncrementDeviceConnected();

                // Delay first event by a random amount to avoid bursts
                crtInterval = GetCurrentInterval(this.interval, 0, counter, out newcounter);

                await Task.Delay(this.random.Next(crtInterval), cancellationToken);

                var stopwatch = new Stopwatch();
                stopwatch.Start();
                for (var i = 0L; !cancellationToken.IsCancellationRequested && (this.config.MessageCount <= 0 || i < this.config.MessageCount); i++)
                {
                    await this.sender.SendMessageAsync(stats, cancellationToken);
                    crtInterval = GetCurrentInterval(this.interval, crtInterval, counter, out newcounter);
                    counter = newcounter;
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

            // This has to return the new interval sum and the counter
            int GetCurrentInterval(object interval, int previousInterval, int counter, out int newcounter)
            {
                newcounter = counter;
                int crtInterval = 0;
                if (this.interval is int)
                {
                    crtInterval = (int)this.interval;
                }
                else if (this.interval is List<int>)
                {
                    IList<int> collection = (List<int>)this.interval;
                    newcounter++;
                    if (counter >= collection.Count)
                    {
                        newcounter = 0;
                    }

                    crtInterval = (int)collection[newcounter];
                }

                return previousInterval + crtInterval;
            }
        }
    }
}
