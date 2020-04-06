namespace IotTelemetrySimulator
{
    public interface IDeviceSimulatorFactory
    {
        SimulatedDevice Create(string deviceId, RunnerConfiguration config);
    }
}