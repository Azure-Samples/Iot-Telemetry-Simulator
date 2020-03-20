using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IotTelemetrySimulator
{
    class SimulationWorker : IHostedService
    {
        private readonly IDeviceSimulatorFactory deviceSimulatorFactory;
        private readonly IHostApplicationLifetime applicationLifetime;
        private CancellationTokenSource stopping;
        private RunnerConfiguration config;
        private RunnerStats stats;
        private List<SimulatedDevice> devices;
        private Task runner;

        public SimulationWorker(IDeviceSimulatorFactory deviceSimulatorFactory,
                                IHostApplicationLifetime applicationLifetime,
                                IConfiguration configuration,
                                ILogger<SimulationWorker> logger)
        {
            this.deviceSimulatorFactory = deviceSimulatorFactory;
            this.applicationLifetime = applicationLifetime;
            stopping = new CancellationTokenSource();
            config = RunnerConfiguration.Load(configuration, logger);
            config.EnsureIsValid();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {                        
            devices = new List<SimulatedDevice>(config.DeviceCount);

            IEnumerable<string> deviceIdentifiers = null;

            if ((config.DeviceList?.Count ?? 0) > 0)
            {
                deviceIdentifiers = config.DeviceList;
            }
            else
            {
                deviceIdentifiers = Enumerable.Range(config.DeviceIndex, config.DeviceCount)
                                              .Select(n => string.Concat(config.DevicePrefix, n.ToString("000000", CultureInfo.InvariantCulture)));
            }

            foreach (var deviceId in deviceIdentifiers)
            {               
                devices.Add(deviceSimulatorFactory.Create(deviceId, config));
            }

            runner = Task.Run(RunnerAsync);

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
                Console.WriteLine("Device count = " + config.DeviceCount);
                Console.WriteLine($"Device prefix = {config.DevicePrefix}");
                Console.WriteLine($"Device 0-last = ({devices[0].DeviceID}-{devices.Last().DeviceID})");
                Console.WriteLine("Device index = " + config.DeviceIndex);
                Console.WriteLine("Message count = " + config.MessageCount);
                Console.WriteLine("Interval = " + config.Interval + "ms");
                Console.WriteLine("Template = " + config.PayloadGenerator.GetDescription());
                Console.WriteLine("Header = " + config.Header?.GetTemplateDefinition());
                Console.WriteLine("========================================================================================================================");


                stats = new RunnerStats();
                await Task.WhenAll(devices.Select(x => x.Start(stats, stopping.Token)));

                timer.Stop();
                Console.WriteLine($"{DateTime.UtcNow.ToString("o")}: Errors sending telemetry == {stats.TotalSendTelemetryErrors}");
                Console.WriteLine($"{DateTime.UtcNow.ToString("o")}: Telemetry generation ended after {timer.ElapsedMilliseconds}ms");
            }
            catch (OperationCanceledException) when (stopping.Token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }

            applicationLifetime.StopApplication();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            const int maxWaitTime = 5_000;

            stopping.Cancel();

            if (runner != null)
            {
                using (var cts = new CancellationTokenSource(maxWaitTime))
                {
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token))
                    {
                        try
                        {
                            await Task.WhenAny(runner, Task.Delay(Timeout.Infinite, linkedCts.Token));
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }
            }
        }
    }
}
