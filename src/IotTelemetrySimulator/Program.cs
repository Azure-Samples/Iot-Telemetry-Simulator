using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IotTelemetrySimulator
{


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
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IDeviceSimulatorFactory, DefaultDeviceSimulatorFactory>();
                    services.AddHostedService<SimulationWorker>();
                });
        }
    }
}
