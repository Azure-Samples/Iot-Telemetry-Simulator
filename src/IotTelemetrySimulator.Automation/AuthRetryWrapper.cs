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
        private readonly Func<Task<IAzure>> _azureFactory;
        private readonly IAzure _existingInstance;
        private readonly ILogger _logger;
        private TaskCompletionSource<IAzure> _tokenRefresh;
        private readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);
        private DateTime _lastRefresh;

        public AuthRetryWrapper(Func<Task<IAzure>> azureFactory, ILogger logger, IAzure existingInstance = null)
        {
            _azureFactory = azureFactory;
            _logger = logger;
            _existingInstance = existingInstance;
        }

        public async Task ExecuteAsync(Action<IAzure> func, CancellationToken cancellationToken = default)
        {
            var azure = _existingInstance ?? await _azureFactory();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Run(() => func(azure), cancellationToken);
                    return;
                }
                catch (AggregateException ex) when (IsRetryAllowed() && ex.Flatten().InnerExceptions.FirstOrDefault(ex => ex is CloudException) != null)
                {
                    azure = await RefreshToken();
                }
                catch (CloudException) when (IsRetryAllowed())
                {
                    azure = await RefreshToken();
                }
            }
        }

        private Task<IAzure> RefreshToken()
        {
            _tokenSemaphore.Wait();

            try
            {
                var localRefreshToken = _tokenRefresh;
                if (localRefreshToken == null)
                {
                    localRefreshToken = _tokenRefresh = new TaskCompletionSource<IAzure>();

                    IAzure result = null;

                    _ = Task
                        .Run(async () =>
                        {
                            _logger.Log(LogLevel.Information, $"Re-authenticating to azure");
                            _lastRefresh = DateTime.UtcNow;
                            result = await _azureFactory();
                        })
                        .ContinueWith((t) =>
                        {
                            if (t.IsCompletedSuccessfully)
                            {
                                _tokenRefresh.TrySetResult(result);
                            }
                            else if (t.IsCanceled)
                            {
                                _tokenRefresh.TrySetCanceled();
                            }
                            else if (t.IsFaulted)
                            {
                                _tokenRefresh.TrySetException(t.Exception);
                            }

                            _tokenRefresh = null;
                        });
                }

                return localRefreshToken.Task;
            }
            finally
            {
                _tokenSemaphore.Release();
            }

        }

        private bool IsRetryAllowed()
        {
            return _tokenRefresh != null || ((DateTime.UtcNow - _lastRefresh) > TimeSpan.FromMinutes(1));
        }
    }
}
