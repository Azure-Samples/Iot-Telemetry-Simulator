namespace IotTelemetrySimulator.Automation
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
    using Microsoft.Extensions.Logging;

    public class IotTelemetrySimulatorAutomation
    {
        private readonly ILogger logger;
        private readonly Configuration configuration;
        private readonly Region region;
        private readonly Dictionary<string, string> envVars;
        private readonly int devicesPerContainer;

        public IotTelemetrySimulatorAutomation(ILogger logger)
        {
            this.logger = logger;
            this.configuration = Configuration.GetConfiguration();
            this.region = Region.Create(this.configuration.AzureRegion);
            this.devicesPerContainer = this.configuration.DeviceCount / this.configuration.ContainerCount;

            this.envVars = new Dictionary<string, string>
            {
                { "IotHubConnectionString", this.configuration.IotHubConnectionString },
                { "EventHubConnectionString", this.configuration.EventHubConnectionString },
                { "KafkaConnectionProperties", this.configuration.KafkaConnectionProperties },
                { "KafkaTopic", this.configuration.KafkaTopic },
                { "PayloadDistribution", this.configuration.PayloadDistribution },
                { "Template1", this.configuration.Template1 },
                { "Template2", this.configuration.Template2 },
                { "Variables", this.configuration.Variables },
                { "DevicePrefix", this.configuration.DevicePrefix },
                { "Header", this.configuration.Header },
                { "PartitionKey", this.configuration.PartitionKey },
                { "MessageCount", this.configuration.MessageCount.ToString() },
                { "Interval", this.configuration.Interval.ToString() },
                { "DuplicateEvery", this.configuration.DuplicateEvery.ToString() },
                { "DeviceCount", this.devicesPerContainer.ToString() },
                { "DeviceIndex", string.Empty }
            };
        }

        public async Task<bool> RunAsync()
        {
            var isSuccess = true;
            var taskCount = 0;
            IAzure azure;

            // Authenticate with Azure
            var azureHelper = new AzureHelper(this.logger, this.configuration);

            if (string.Equals(this.configuration.AuthenticationMethod, "Managed Identity", StringComparison.InvariantCultureIgnoreCase))
            {
                azure = await azureHelper.GetAzureContextFromManagedIdentityAsync();
            }
            else if (string.Equals(this.configuration.AuthenticationMethod, "Service Principal", StringComparison.InvariantCultureIgnoreCase))
            {
                azure = azureHelper.GetAzureContextFromServicePrincipal();
            }
            else
            {
                this.logger.LogError("AuthenticationMethod in configuration needs to be 'Managed Identity' or 'Service Principal'");
                return false;
            }

            // Create a resource group in which the container groups are to be created
            isSuccess &= azureHelper.CreateResourceGroup(azure, this.region);

            if (!isSuccess)
            {
                return false;
            }

            // Create Azure Container Instances
            var taskContainerGroupList = new List<string>();
            var taskList = new Task[this.configuration.ContainerCount];

            var deviceIndex = 1;
            for (var i = 1; i <= Convert.ToInt32(this.configuration.ContainerCount); i++)
            {
                var taskContainerGroupName = this.configuration.ContainerGroupName + "-" + i;
                taskContainerGroupList.Add(taskContainerGroupName);

                var tempVars = new Dictionary<string, string>(this.envVars)
                {
                    ["DeviceIndex"] = deviceIndex.ToString()
                };

                taskList[i - 1] = Task.Run(() =>
                      isSuccess &= azureHelper.RunTaskBasedContainer(azure, taskContainerGroupName, tempVars));

                deviceIndex += this.devicesPerContainer;
            }

            Task.WaitAll(taskList);

            if (isSuccess)
            {
                // Wait for containers to enter "Running" state
                var wrapper = new AuthRetryWrapper(async () => await azureHelper.GetAzureContextFromManagedIdentityAsync(), this.logger, azure);

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
                if (string.Equals(this.configuration.AuthenticationMethod, "Managed Identity", StringComparison.InvariantCultureIgnoreCase))
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
                        azureHelper.GetContainerLogs(azure, taskContainerGroupToAwait));
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
                    azureHelper.DeleteContainerGroup(azure, taskContainerGroupToDelete));
                taskCount++;
            }

            Task.WaitAll(taskList);

            // Delete resource group
            azureHelper.DeleteResourceGroup(azure);

            return isSuccess;
        }
    }
}
