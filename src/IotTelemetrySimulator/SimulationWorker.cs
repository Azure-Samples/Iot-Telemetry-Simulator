namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    class SimulationWorker : IHostedService
    {
        private readonly IDeviceSimulatorFactory deviceSimulatorFactory;
        private readonly IHostApplicationLifetime applicationLifetime;
        private readonly CancellationTokenSource stopping;
        private readonly RunnerConfiguration config;
        private RunnerStats stats;
        private List<SimulatedDevice> devices;
        private Task runner;

        public SimulationWorker(
            IDeviceSimulatorFactory deviceSimulatorFactory,
            IHostApplicationLifetime applicationLifetime,
            IConfiguration configuration,
            ILogger<SimulationWorker> logger)
        {
            this.deviceSimulatorFactory = deviceSimulatorFactory;
            this.applicationLifetime = applicationLifetime;
            this.stopping = new CancellationTokenSource();
            this.config = RunnerConfiguration.Load(configuration, logger);
            this.config.EnsureIsValid();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.devices = new List<SimulatedDevice>(this.config.DeviceCount);

            IEnumerable<string> deviceIdentifiers;

            if ((this.config.DeviceList?.Count ?? 0) > 0)
            {
                deviceIdentifiers = this.config.DeviceList;
            }
            else
            {
                deviceIdentifiers = Enumerable.Range(this.config.DeviceIndex, this.config.DeviceCount)
                                              .Select(n => string.Concat(this.config.DevicePrefix, n.ToString("000000", CultureInfo.InvariantCulture)));
            }

            foreach (var deviceId in deviceIdentifiers)
            {
                this.devices.Add(this.deviceSimulatorFactory.Create(deviceId, this.config));
            }

            this.runner = Task.Run(this.RunnerAsync, cancellationToken);

            return Task.CompletedTask;
        }

        private async Task RunnerAsync()
        {
            var timer = Stopwatch.StartNew();

            try
            {
                Console.WriteLine("========================================================================================================================");
                Console.WriteLine();
                Console.WriteLine($"Starting simulator v{Constants.AppVersion}");
                Console.WriteLine();
                Console.WriteLine("Device count = " + this.config.DeviceCount);
                Console.WriteLine($"Device prefix = {this.config.DevicePrefix}");
                Console.WriteLine($"Device 0-last = ({this.devices[0].DeviceID}-{this.devices.Last().DeviceID})");
                Console.WriteLine("Device index = " + this.config.DeviceIndex);
                Console.WriteLine("Message count = " + this.config.MessageCount);
                Console.WriteLine("Interval = " + this.config.Interval + "ms");
                Console.WriteLine("Template = " + this.config.PayloadGenerator.GetDescription());
                Console.WriteLine("Header = " + this.config.Header?.GetTemplateDefinition());
                Console.WriteLine("========================================================================================================================");

                this.stats = new RunnerStats();
                await Task.WhenAll(this.devices.Select(x => x.Start(this.stats, this.stopping.Token)));

                timer.Stop();

                Console.WriteLine(
                    this.stats.TotalSendTelemetryErrors != 0
                    ? $"{DateTime.UtcNow:o}: Errors sending telemetry == {this.stats.TotalSendTelemetryErrors}"
                    : $"{DateTime.UtcNow:o}: No errors sending telemetry");

                Console.WriteLine($"{DateTime.UtcNow:o}: Telemetry generation ended after {timer.ElapsedMilliseconds}ms");
            }
            catch (OperationCanceledException) when (this.stopping.Token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }

            this.applicationLifetime.StopApplication();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            const int maxWaitTime = 5_000;

            this.stopping.Cancel();

            if (this.runner != null)
            {
                using var cts = new CancellationTokenSource(maxWaitTime);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                try
                {
                    await Task.WhenAny(this.runner, Task.Delay(Timeout.Infinite, linkedCts.Token));
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }
}
