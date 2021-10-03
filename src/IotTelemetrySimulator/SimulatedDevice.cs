﻿namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public class SimulatedDevice
    {
        private readonly ISender sender;
        private readonly int[] intervals;
        private readonly RunnerConfiguration config;
        private readonly IRandomizer random = new DefaultRandomizer();

        public string DeviceID { get; private set; }

        public SimulatedDevice(string deviceId, RunnerConfiguration config, ISender sender)
        {
            this.DeviceID = deviceId;
            this.config = config;
            this.sender = sender;
            this.intervals = config.GetMessageIntervalForDevice(deviceId);
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
                int crtInterval = this.intervals[0];

                await Task.Delay(this.random.Next(crtInterval), cancellationToken);

                var stopwatch = new Stopwatch();
                stopwatch.Start();
                for (var i = 0L; !cancellationToken.IsCancellationRequested && (this.config.MessageCount <= 0 || i < this.config.MessageCount); i++)
                {
                    await this.sender.SendMessageAsync(stats, cancellationToken);
                    crtInterval = this.intervals[i % this.intervals.Length];
                    var millisecondsDelay = Math.Max(0, crtInterval - stopwatch.ElapsedMilliseconds);
                    await Task.Delay((int)millisecondsDelay, cancellationToken);
                    stopwatch.Restart();
                }

                stats.IncrementCompletedDevice();
                stopwatch.Stop();
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