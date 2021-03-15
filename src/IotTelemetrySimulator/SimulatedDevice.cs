﻿namespace IotTelemetrySimulator
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public class SimulatedDevice
    {
        private readonly ISender sender;
        private readonly RunnerConfiguration config;
        private readonly IRandomizer random = new DefaultRandomizer();

        public string DeviceID { get; }

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

        private async Task RunnerAsync(RunnerStats stats, CancellationToken cancellationToken)
        {
            try
            {
                await this.sender.OpenAsync();
                stats.IncrementDeviceConnected();

                // Delay first event by a random amount to avoid bursts
                await Task.Delay(this.random.Next(this.config.Interval), cancellationToken);

                var stopwatch = new Stopwatch();
                stopwatch.Start();
                for (var i = 0L; !cancellationToken.IsCancellationRequested && (this.config.MessageCount <= 0 || i < this.config.MessageCount); i++)
                {
                    await this.sender.SendMessageAsync(stats, cancellationToken);

                    var millisecondsDelay = Math.Max(0, this.config.Interval * i - stopwatch.ElapsedMilliseconds);
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
