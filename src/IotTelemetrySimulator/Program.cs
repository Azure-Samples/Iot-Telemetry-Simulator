namespace IotTelemetrySimulator
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
#if DEBUG
                .UseEnvironment("Development")
#endif
                .ConfigureAppConfiguration(builder =>
                {
                    var tempConfig = builder.Build();
                    var fileConfig = tempConfig["File"];
                    if (!string.IsNullOrEmpty(fileConfig))
                    {
                        builder.AddJsonFile(fileConfig, false, false);
                    }

                    if (tempConfig is IDisposable diposableConfig)
                    {
                        diposableConfig.Dispose();
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IDeviceSimulatorFactory, DefaultDeviceSimulatorFactory>();
                    services.AddHostedService<SimulationWorker>();
                });
        }
    }
}
