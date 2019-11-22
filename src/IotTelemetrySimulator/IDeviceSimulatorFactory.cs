namespace IotTelemetrySimulator
{
    public interface IDeviceSimulatorFactory
    {
        SimulatedDevice Create(int deviceNumber, RunnerConfiguration config);
    }
}