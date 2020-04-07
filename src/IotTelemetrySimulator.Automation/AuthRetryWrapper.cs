namespace IotTelemetrySimulator.Automation
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.Fluent;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest.Azure;

    internal class AuthRetryWrapper
    {
        private readonly Func<Task<IAzure>> azureFactory;
        private readonly IAzure existingInstance;
        private readonly ILogger logger;
        private readonly SemaphoreSlim tokenSemaphore = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<IAzure> tokenRefresh;
        private DateTime lastRefresh;

        public AuthRetryWrapper(Func<Task<IAzure>> azureFactory, ILogger logger, IAzure existingInstance = null)
        {
            this.azureFactory = azureFactory;
            this.logger = logger;
            this.existingInstance = existingInstance;
        }

        public async Task ExecuteAsync(Action<IAzure> func, CancellationToken cancellationToken = default)
        {
            var azure = this.existingInstance ?? await this.azureFactory();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Run(() => func(azure), cancellationToken);
                    return;
                }
                catch (AggregateException ex) when (this.IsRetryAllowed() && ex.Flatten().InnerExceptions.FirstOrDefault(ex => ex is CloudException) != null)
                {
                    azure = await this.RefreshToken();
                }
                catch (CloudException) when (this.IsRetryAllowed())
                {
                    azure = await this.RefreshToken();
                }
            }
        }

        private Task<IAzure> RefreshToken()
        {
            this.tokenSemaphore.Wait();

            try
            {
                var localRefreshToken = this.tokenRefresh;
                if (localRefreshToken == null)
                {
                    localRefreshToken = this.tokenRefresh = new TaskCompletionSource<IAzure>();

                    IAzure result = null;

                    _ = Task
                        .Run(async () =>
                        {
                            this.logger.Log(LogLevel.Information, $"Re-authenticating to azure");
                            this.lastRefresh = DateTime.UtcNow;
                            result = await this.azureFactory();
                        })
                        .ContinueWith((t) =>
                        {
                            if (t.IsCompletedSuccessfully)
                            {
                                this.tokenRefresh.TrySetResult(result);
                            }
                            else if (t.IsCanceled)
                            {
                                this.tokenRefresh.TrySetCanceled();
                            }
                            else if (t.IsFaulted)
                            {
                                this.tokenRefresh.TrySetException(t.Exception);
                            }

                            this.tokenRefresh = null;
                        });
                }

                return localRefreshToken.Task;
            }
            finally
            {
                this.tokenSemaphore.Release();
            }
        }

        private bool IsRetryAllowed()
        {
            return this.tokenRefresh != null || ((DateTime.UtcNow - this.lastRefresh) > TimeSpan.FromMinutes(1));
        }
    }
}
