using System.Threading;
using System.Threading.Tasks;

namespace IotTelemetrySimulator
{
    public interface ISender
    {
        Task OpenAsync();

        Task SendMessageAsync(RunnerStats stats, CancellationToken cancellationToken);
    }
}
