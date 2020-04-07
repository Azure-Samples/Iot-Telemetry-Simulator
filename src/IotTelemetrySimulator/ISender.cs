namespace IotTelemetrySimulator
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISender
    {
        Task OpenAsync();

        Task SendMessageAsync(RunnerStats stats, CancellationToken cancellationToken);
    }
}
