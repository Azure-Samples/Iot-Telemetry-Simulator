namespace IotSimulatorDeviceProvisioning
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;

    class Program
    {
        const int ParallelizationLevel = 4;
        const int CreateOperationBulkSize = 50;
        const string IotHubConnectionStringEnvVar = "IotHubConnectionString";
        const string DevicePrefixEnvVar = "DevicePrefix";
        const string DeviceCountEnvVar = "DeviceCount";
        const string DeviceIndexEnvVar = "DeviceIndex";

        const string OperationNameEnvVar = "Operation";
        const string DeleteOperationNameEnvVar = "Delete";
        const string DeleteConfirmationEnvVar = "ConfirmDelete";
        const string DeleteConfirmationResponseEnvVar = "yes";

        static async Task<int> Main(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable(IotHubConnectionStringEnvVar);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine($"Environment variable for '{IotHubConnectionStringEnvVar}' is not defined");
                return -1;
            }

            var devicePrefix = Environment.GetEnvironmentVariable(DevicePrefixEnvVar);
            if (string.IsNullOrWhiteSpace(devicePrefix))
            {
                devicePrefix = "sim";
            }

            if (!int.TryParse(Environment.GetEnvironmentVariable(DeviceCountEnvVar), out var deviceCount) || deviceCount < 1)
            {
                deviceCount = 1;
            }

            if (!int.TryParse(Environment.GetEnvironmentVariable(DeviceIndexEnvVar), out var deviceIndex) || deviceIndex < 0)
            {
                deviceIndex = 1;
            }

            bool isCreateOperation = true;

            if (string.Equals(Environment.GetEnvironmentVariable(OperationNameEnvVar), DeleteOperationNameEnvVar, StringComparison.OrdinalIgnoreCase))
            {
                var isDeleteConfirmed = string.Equals(Environment.GetEnvironmentVariable(DeleteConfirmationEnvVar), DeleteConfirmationResponseEnvVar, StringComparison.OrdinalIgnoreCase);
                if (!isDeleteConfirmed)
                {
                    Console.WriteLine($"Delete operations must be confirmed with an additional environment variable {DeleteConfirmationEnvVar}={DeleteConfirmationResponseEnvVar}");
                    return -1;
                }

                isCreateOperation = false;
            }

            RegistryManager registryManager = null;

            try
            {
                registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed connecting to IoT Hub registry, check the connection string value\n" + ex.ToString());
                return 1;
            }

            try
            {
                await registryManager.OpenAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed connecting to IoT Hub registry.\n" + ex.ToString());
                return 1;
            }

            var deviceIdCollection = Enumerable.Range(deviceIndex, deviceCount)
                .Select(n => $"{devicePrefix}{n.ToString("000000")}");

            var registryManagerTasks = new List<Task>();

            var stats = new DeviceProvisionStats();

            if (isCreateOperation)
            {
                Console.WriteLine($"Starting device provisioning");
            }
            else
            {
                Console.WriteLine($"Starting device deletion");
            }

            var timer = Stopwatch.StartNew();

            foreach (var partition in Partitioner.Create(deviceIdCollection).GetPartitions(ParallelizationLevel))
            {
                if (isCreateOperation)
                {
                    registryManagerTasks.Add(Task.Run(() => CreateDevicesAsync(partition, registryManager, stats)));
                }
                else
                {
                    registryManagerTasks.Add(Task.Run(() => DeleteDevicesAsync(partition, registryManager, stats)));
                }
            }

            try
            {
                await Task.WhenAll(registryManagerTasks);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }

            timer.Stop();
            Console.WriteLine($"Finished device provisioning");
            Console.WriteLine($"Device count = {deviceCount}");
            Console.WriteLine($"Total devices created = {stats.TotalCreated}");
            Console.WriteLine($"Total devices deleted = {stats.TotalDeleted}");
            Console.WriteLine($"Total errors = {stats.TotalErrors}");
            Console.WriteLine($"Time = {timer.ElapsedMilliseconds}ms");

            return 0;
        }

        private static async Task CreateDevicesAsync(IEnumerator<string> deviceIds, RegistryManager registryManager, DeviceProvisionStats stats)
        {
            var devices = new List<Device>();
            while (deviceIds.MoveNext())
            {
                var deviceId = deviceIds.Current;

                devices.Add(new Device(deviceId));

                if (devices.Count == CreateOperationBulkSize)
                {
                    await BulkCreateDevicesAsync(devices, registryManager, stats);

                    devices.Clear();
                }
            }

            if (devices.Count > 0)
            {
                await BulkCreateDevicesAsync(devices, registryManager, stats);
            }
        }

        private static async Task BulkCreateDevicesAsync(List<Device> devices, RegistryManager registryManager, DeviceProvisionStats stats)
        {
            try
            {
                var bulkResult = await registryManager.AddDevices2Async(devices);
                var totalErrors = bulkResult?.Errors?.Length ?? 0;
                var totalCreated = devices.Count - totalErrors;
                if (totalCreated > 0)
                {
                    stats.IncrementCreated(totalCreated);
                }

                if (totalErrors > 0)
                {
                    stats.IncrementErrors(totalErrors);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        private static async Task DeleteDevicesAsync(IEnumerator<string> deviceIds, RegistryManager registryManager, DeviceProvisionStats stats)
        {
            while (deviceIds.MoveNext())
            {
                var deviceId = deviceIds.Current;

                try
                {
                    await registryManager.RemoveDeviceAsync(deviceId);
                    stats.IncrementDeleted(1);
                }
                catch
                {
                    stats.IncrementErrors(1);
                }
            }
        }
    }
}
