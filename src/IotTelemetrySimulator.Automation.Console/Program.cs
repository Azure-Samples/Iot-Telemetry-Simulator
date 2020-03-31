namespace IotTelemetrySimulator.Automation.Console
{
    using IotTelemetrySimulator.Automation;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    class Program
    {
        private enum ExitCode : int
        {
            Error = -1,
            Success = 0,
        }

        static async Task<int> Main()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddConsole()
                    .AddDebug();
            });
            ILogger logger = loggerFactory.CreateLogger<Program>();

            var iotTelemetrySimulatorAutomation = new IotTelemetrySimulatorAutomation(logger);
            return (int)(await iotTelemetrySimulatorAutomation.RunAsync() ? ExitCode.Success : ExitCode.Error);
        }
    }
}
