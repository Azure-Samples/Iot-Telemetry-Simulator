namespace IotTelemetrySimulator
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    internal class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
#if DEBUG
                .UseEnvironment("Development")
#endif
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IDeviceSimulatorFactory, DefaultDeviceSimulatorFactory>();
                    services.AddHostedService<SimulationWorker>();
                });
        }
    }
}
