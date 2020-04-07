namespace IotTelemetrySimulator.Automation
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.ContainerInstance.Fluent;
    using Microsoft.Azure.Management.ContainerInstance.Fluent.ContainerGroup.Definition;
    using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
    using Microsoft.Azure.Management.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.Extensions.Logging;

    public class AzureHelper
    {
        private readonly ILogger logger;
        private readonly Configuration configuration;

        public AzureHelper(ILogger logger, Configuration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        public IAzure GetAzureContextFromServicePrincipal()
        {
            IAzure azure;
            ISubscription subscription;

            var servicePrincipalLoginInformation = new ServicePrincipalLoginInformation()
            {
                ClientId = this.configuration.AadClientId,
                ClientSecret = this.configuration.AadClientSecret,
            };

            var environment = new AzureEnvironment()
            {
                AuthenticationEndpoint = Constants.AadEndpointUrl,
                GraphEndpoint = Constants.AadGraphResourceId,
                KeyVaultSuffix = Constants.KeyVaultSuffix,
                ManagementEndpoint = Constants.ManagementEndpointUrl,
                Name = this.configuration.AzureSubscriptionName,
                ResourceManagerEndpoint = Constants.ResourceManagerEndpointUrl,
                StorageEndpointSuffix = Constants.StorageEndpointSuffix,
            };

            var azureCredentials = new AzureCredentials(servicePrincipalLoginInformation, this.configuration.AadTenantId, environment);

            try
            {
                this.logger.Log(LogLevel.Information, $"Authenticating with Azure");

                azure = Azure.Authenticate(azureCredentials).WithDefaultSubscription();
                subscription = azure.GetCurrentSubscription();
            }
            catch (Exception ex)
            {
                this.logger.Log(LogLevel.Error, $"Failed to authenticate with Azure: {ex.Message}");
                throw;
            }

            this.logger.Log(LogLevel.Debug, $"Successfully authenticated with Azure subscription {subscription.DisplayName}");

            return azure;
        }

        public async Task<IAzure> GetAzureContextFromManagedIdentityAsync()
        {
            IAzure azure;
            ISubscription subscription;

            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/").ConfigureAwait(false);
            var restTokenCredentials = new Microsoft.Rest.TokenCredentials(accessToken);
            var azCred = new AzureCredentials(restTokenCredentials, null, this.configuration.AadTenantId, AzureEnvironment.AzureGlobalCloud);
            var rest = RestClient.Configure().WithEnvironment(AzureEnvironment.AzureGlobalCloud).WithCredentials(azCred).Build();

            try
            {
                this.logger.LogDebug($"Authenticating with Azure");
                azure = Azure.Authenticate(rest, this.configuration.AadTenantId).WithSubscription(this.configuration.AzureSubscriptionId);
                subscription = azure.GetCurrentSubscription();
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Failed to authenticate with Auzre: {ex.Message}");
                throw;
            }

            this.logger.Log(LogLevel.Debug, $"Successfully authenticated with Azure subscription {subscription.DisplayName}");

            return azure;
        }

        public bool CreateResourceGroup(IAzure azure, Region azureRegion)
        {
            this.logger.Log(LogLevel.Information, $"Creating resource group '{this.configuration.ResourceGroupName}'");

            IResourceGroup resourceGroupCreated;

            try
            {
                resourceGroupCreated = azure.ResourceGroups.Define(this.configuration.ResourceGroupName)
                    .WithRegion(azureRegion)
                    .Create();
            }
            catch (Exception ex)
            {
                this.logger.Log(LogLevel.Error, $"Error creating resource group '{this.configuration.ResourceGroupName}': {ex.Message}");
                throw;
            }

            if (resourceGroupCreated.ProvisioningState != Microsoft.Azure.Management.AppService.Fluent.Models.ProvisioningState.Succeeded.ToString())
            {
                this.logger.Log(LogLevel.Error, $"Failed to create resource group '{this.configuration.ResourceGroupName}' with state {resourceGroupCreated.ProvisioningState}");
                return false;
            }

            return true;
        }

        public bool RunTaskBasedContainer(IAzure azure, string containerGroupName, Dictionary<string, string> envVars)
        {
            this.logger.Log(LogLevel.Information, $"Creating container group '{containerGroupName}'");

            IContainerGroup containerGroup;

            // Create the container group
            try
            {
                var configStep1 = azure.ContainerGroups.Define(containerGroupName)
                    .WithRegion(this.configuration.AzureRegion)
                    .WithExistingResourceGroup(this.configuration.ResourceGroupName)
                    .WithLinux();

                IWithPrivateImageRegistryOrVolume configStep2;

                if (string.IsNullOrEmpty(this.configuration.AcrServer))
                {
                    configStep2 = configStep1.WithPublicImageRegistryOnly();
                }
                else
                {
                    configStep2 = configStep1.WithPrivateImageRegistry(this.configuration.AcrServer, this.configuration.AcrUsername, this.configuration.AcrPassword);
                }

                containerGroup = configStep2.WithoutVolume()
                    .DefineContainerInstance(containerGroupName + "-container")
                    .WithImage(this.configuration.ContainerImage)
                    .WithExternalTcpPort(80)
                    .WithCpuCoreCount(this.configuration.CpuCores)
                    .WithMemorySizeInGB(this.configuration.MemoryGb)
                    .WithEnvironmentVariables(envVars)
                    .Attach()
                    .WithDnsPrefix(containerGroupName)
                    .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
                    .Create();
            }
            catch (Exception ex)
            {
                this.logger.Log(LogLevel.Error, $"Error creating container group '{containerGroupName}': {ex.Message}");
                throw;
            }

            if (containerGroup.ProvisioningState != Microsoft.Azure.Management.AppService.Fluent.Models.ProvisioningState.Succeeded.ToString())
            {
                this.logger.Log(LogLevel.Error, $"Failed to create container group '{containerGroupName}' with state {containerGroup.ProvisioningState}");
                return false;
            }

            return true;
        }

        public void WaitForContainerGroupToEnterState(IAzure azure, string containerGroupName, string state)
        {
            var hasAllReachedState = false;

            while (!hasAllReachedState)
            {
                IContainerGroup containerGroup = null;

                while (containerGroup == null)
                {
                    containerGroup = azure.ContainerGroups.GetByResourceGroup(this.configuration.ResourceGroupName, containerGroupName);
                    SdkContext.DelayProvider.Delay(Constants.Delay);
                }

                var containersReachedState = 0;

                foreach (var container in containerGroup.Containers.Values)
                {
                    this.logger.Log(LogLevel.Information, $"Waiting for container '{container.Name}' to reach state: '{state}'");

                    if (container.InstanceView.CurrentState.State == state)
                    {
                        containersReachedState++;
                        this.logger.Log(LogLevel.Debug, $"Container '{container.Name}' has reached state: '{state}'");
                    }
                }

                if (containersReachedState == containerGroup.Containers.Count)
                {
                    hasAllReachedState = true;
                }
            }
        }

        public void GetContainerLogs(IAzure azure, string containerGroupName)
        {
            IContainerGroup containerGroup = null;

            while (containerGroup == null)
            {
                containerGroup = azure.ContainerGroups.GetByResourceGroup(this.configuration.ResourceGroupName, containerGroupName);
                SdkContext.DelayProvider.Delay(Constants.Delay);
            }

            foreach (var container in containerGroup.Containers.Values)
            {
                // Print the container's logs
                this.logger.Log(LogLevel.Debug, $"Logs for container '{container.Name}':");
                this.logger.Log(LogLevel.Debug, containerGroup.GetLogContent(container.Name));
                this.logger.Log(LogLevel.Debug, $"End logs for container '{container.Name}'");
            }
        }

        public void DeleteContainerGroup(IAzure azure, string containerGroupName)
        {
            IContainerGroup containerGroup = null;

            while (containerGroup == null)
            {
                containerGroup = azure.ContainerGroups.GetByResourceGroup(this.configuration.ResourceGroupName, containerGroupName);
                SdkContext.DelayProvider.Delay(Constants.Delay);
            }

            this.logger.Log(LogLevel.Information, $"Deleting container group '{containerGroupName}'");

            try
            {
                azure.ContainerGroups.DeleteById(containerGroup.Id);
            }
            catch (Exception ex)
            {
                this.logger.Log(LogLevel.Error, $"Error deleting container group '{containerGroupName}': {ex.Message}");
                if (ExceptionHandler.IsFatal(ex))
                {
                    throw;
                }
            }
        }

        public void DeleteResourceGroup(IAzure azure)
        {
            this.logger.Log(LogLevel.Information, $"Deleting resource group '{this.configuration.ResourceGroupName}'");
            try
            {
                azure.ResourceGroups.DeleteByName(this.configuration.ResourceGroupName);
            }
            catch (Exception ex)
            {
                this.logger.Log(LogLevel.Error, $"Error deleting resource group'{this.configuration.ResourceGroupName}': {ex.Message}");
                if (ExceptionHandler.IsFatal(ex))
                {
                    throw;
                }
            }
        }
    }
}
