namespace IotTelemetrySimulator.Automation
{
    using Microsoft.Azure.Management.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class IotTelemetrySimulatorAutomation
    {
        private readonly ILogger _logger;
        private readonly Configuration _configuration;
        private readonly Region _region;
        private readonly Dictionary<string, string> _envVars;
        private readonly int _devicesPerContainer;

        public IotTelemetrySimulatorAutomation(ILogger logger)
        {
            _logger = logger;
            _configuration = Configuration.GetConfiguration();
            _region = Region.Create(_configuration.AzureRegion);
            _devicesPerContainer = _configuration.DeviceCount / _configuration.ContainerCount;

            _envVars = new Dictionary<string, string>
            {
                { "IotHubConnectionString", _configuration.IotHubConnectionString },
                { "EventHubConnectionString", _configuration.EventHubConnectionString },
                { "PayloadDistribution", _configuration.PayloadDistribution },
                { "Template1", _configuration.Template1 },
                { "Template2", _configuration.Template2 },
                { "Variables", _configuration.Variables },
                { "DevicePrefix", _configuration.DevicePrefix },
                { "Header", _configuration.Header },
                { "MessageCount", _configuration.MessageCount.ToString() },
                { "Interval", _configuration.Interval.ToString() },
                { "DeviceCount", _devicesPerContainer.ToString() },
                { "DeviceIndex", string.Empty }
            };
        }

        public async Task<bool> RunAsync()
        {
            var isSuccess = true;
            var taskCount = 0;
            IAzure azure;

            // Authenticate with Azure
            var azureHelper = new AzureHelper(_logger, _configuration);
            
            if (string.Equals(_configuration.AuthenticationMethod, "Managed Identity", StringComparison.InvariantCultureIgnoreCase))
            {
                azure = await azureHelper.GetAzureContextFromManagedIdentityAsync();
            }
            else if (string.Equals(_configuration.AuthenticationMethod, "Service Principal", StringComparison.InvariantCultureIgnoreCase))
            {
                azure = azureHelper.GetAzureContextFromServicePrincipal();
            }
            else
            {
                _logger.LogError("AuthenticationMethod in configuration needs to be 'Managed Identity' or 'Service Principal'");
                return false;
            }

            // Create a resource group in which the container groups are to be created
            isSuccess &= azureHelper.CreateResourceGroup(azure, _region);

            if (!isSuccess)
            {
                return false;
            }
 
            // Create Azure Container Instances
            var taskContainerGroupList = new List<string>();
            var taskList = new Task[_configuration.ContainerCount];

            var deviceIndex = 1;
            for (var i = 1; i <= Convert.ToInt32(_configuration.ContainerCount); i++)
            {
                var taskContainerGroupName = _configuration.ContainerGroupName + "-" + i;
                taskContainerGroupList.Add(taskContainerGroupName);

                var tempVars = new Dictionary<string, string>(_envVars)
                {
                    ["DeviceIndex"] = deviceIndex.ToString()
                };

                taskList[i - 1] = Task.Run( () =>
                      isSuccess &= azureHelper.RunTaskBasedContainer(azure, taskContainerGroupName, tempVars)
                );

                deviceIndex += _devicesPerContainer;
            }

            Task.WaitAll(taskList);

            if (isSuccess)
            {
                // Wait for containers to enter "Running" state
                var wrapper = new AuthRetryWrapper(async () => await azureHelper.GetAzureContextFromManagedIdentityAsync(), _logger, azure);

                taskList = new Task[taskContainerGroupList.Count];
                
                foreach (var taskContainerGroupToAwait in taskContainerGroupList)
                {
                    taskList[taskCount++] = wrapper.ExecuteAsync((azure) => azureHelper.WaitForContainerGroupToEnterState(azure, taskContainerGroupToAwait, "Running"));
                }
                Task.WaitAll(taskList);

                // Wait for containers to enter "Terminated" state
                taskList = new Task[taskContainerGroupList.Count];
                taskCount = 0;
                foreach (var taskContainerGroupToAwait in taskContainerGroupList)
                {
                    taskList[taskCount++] = wrapper.ExecuteAsync((azure) => azureHelper.WaitForContainerGroupToEnterState(azure, taskContainerGroupToAwait, "Terminated"));
                }

                Task.WaitAll(taskList);

                // The previous task to wait for terminate state can take a long time and cause the auth token to expire.
                // It is re-created here to ensure we can cleanup properly.
                if (string.Equals(_configuration.AuthenticationMethod, "Managed Identity", StringComparison.InvariantCultureIgnoreCase))
                {
                    azure = await azureHelper.GetAzureContextFromManagedIdentityAsync();
                }
                else
                {
                    azure = azureHelper.GetAzureContextFromServicePrincipal();
                }

                // Read container logs
                taskList = new Task[taskContainerGroupList.Count];
                taskCount = 0;
                foreach (var taskContainerGroupToAwait in taskContainerGroupList)
                {
                    taskList[taskCount] = Task.Run(() =>
                        azureHelper.GetContainerLogs(azure, taskContainerGroupToAwait)
                    );
                    taskCount++;
                }
                Task.WaitAll(taskList);
            }

            // Clean up container groups
            taskList = new Task[taskContainerGroupList.Count];
            taskCount = 0;
            foreach (var taskContainerGroupToDelete in taskContainerGroupList)
            {
                taskList[taskCount] = Task.Run(() =>
                    azureHelper.DeleteContainerGroup(azure, taskContainerGroupToDelete)
                );
                taskCount++;
            }
            Task.WaitAll(taskList);

            // Delete resource group
            azureHelper.DeleteResourceGroup(azure);

            return isSuccess;
        }
    }
}
