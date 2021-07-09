# Automation

## Overview

The `IotTelemtrySimulator.Automation` .NET Standard 2.1 library allows you to run the IoT Telemetry Simulator as part of a pipeline or any other automation framework.

It authenticates with Azure either using managed identities or a service principal, sets up the Azure resource group, container group(s) and container instance(s) needed, pulls the IotTelemetrySimulator Docker container from an Azure container registry, configures and runs the simulator, waits for it to finish, pulls and outputs it's log files and then tears down the Container instances and groups and the resource group.

![IotTelemetrySimulator Automation Architecture](/docs/images/iottelemetrysimulator_automation_architecture.png)

This way, automated load tests can be run as part of a CD/CI pipeline or any automation framework that allows executing arbitrary code.

## Project Structure

[IotTelemetrySimulator.Atuomation](/src/IotTelemetrySimulator.Automation) : The .NET Standard 2.1 automation library.

* [appsettings.json](/src/IotTelemetrySimulator.Automation/appsettings.json) : Configuration file.

[IotTelemetrySimulator.Atuomation.Console](/src/IotTelemetrySimulator.Automation.Console) : A .NET Core 3.1 console app that illustrates how to use the automation component.

## Setup

If you chose *Service Principal* authentication, you need to create an Active Directory application with the rights to create and remove resources in the desired Azure subscription. To achieve this, create a new application in Azure Active Directory and then navigate to "Subscriptions &rarr; [Subscription Id] &rarr; Access Control (IAM) &rarr; Role Assignments" and then add the newly created AAD Application with "Contributor" role.

If you chose *Managed Identity* authentication, the application will run with the permissions of the currently logged in user or the Azure DevOps pipeline's permissions. Make sure that you are logged in to the proper Azure account using the [AZ command line interface tool](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest).

To use *Service Principal* authentication, in the configuration, put **AuthenticationMethod = 'Service Principal'** and provide the first four following settings: **AadClientId**, **AadClientSecret**, **AzureSubscriptionname** and **AadTenantId**.

To use *Managed identity* authentication, in the configuration, put **AuthenticationMehtod = 'Managed Identity'** and provide the following two settings: **AadTenantId** and **AzureSubscriptionId**.

## Using Azure Container Registry vs. the Microsoft Container Registry

To have the Azure Container Instance pull the image from an Azure Container Registry, set the **ContainerImage** setting to the full image name including the registry: `acrname.azurecr.io/azureiot-telemetrysimulator:latest` and provide the settings for **AcrServer**, **AcrUsername** and **AcrPassword**.

To use the image published to the Microsoft Container Registry, set the **ContainerImage** to the full image name including the registry again: `mcr.microsoft.com/oss/azure-samples/azureiot-telemetrysimulator:latest` but leave the settings for **AcrServer**, **AcrUsername** and **AcrPassword** empty.

## Configuration

The configuration for the automation component is kept in [appsettings.json](/src/IotTelemetrySimulator.Automation/appsettings.json) or for local development purposes in **appsettings.Development.json**. It contains the following fields:

|Parameter|Description|
|-|-|
|AuthenticationMethod|*Service Principal* or *Managed Identity*|
|AadClientId|Azure Active Directory app registration Application (Client) Id<br/>Only used with Service Principal authentication method|
|AadClientSecret|Azure Active Directory app registration Client Secret<br/>Only used with Service Principal authentication method|
|AzureSubscriptionName|Azure Subscription Name<br/>Only used with Service Principal authentication method|
|AadTenantId|Azure Active Directory Tenant Id (Get by navigating to<br/> "Azure &rarr; Active Directory &rarr; Manage &rarr; Properties &rarr; Directory ID")<br/>Used with Service Principal and Managed Identity authentication methods|
|AzureSubscriptionId|Azure Subscription Id<br/>Only used with Managed Identity authentication method|
|AzureRegion|Azure Region as formatted in the Fluent SDK [Core.Region Class' Fields](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.management.resourcemanager.fluent.core.region?view=azure-dotnet)<br/>All resources will be created in this region.|
|ResourceGroupName|The name of the Azure Resource Group created by to host the Container Groups|
|ContainerGroupName|The name prefix under which the Azure Container Groups will be created|
|ContainerImage|The full path to the docker image to be pulled by the Azure Container Instances.<br/> For example: `acrname.azurecr.io/azureiot-telemetrysimulator:latest`<br/> to use an Azure Container Registry or `mcr.microsoft.com/oss/azure-samples/azureiot-telemetrysimulator:latest`<br/> to use a public container registry / DockerHub|
|AcrServer|The name of the Azure Container Registry. I.e. `acrname.azurecr.io<br/>Leave blank to use a public container registry|
|AcrUsername|The name of user with which to access the Azure Container Registry<br/>Leave blank to use a public container registry|
|AcrPassword|The above user's Azure Container Registry password<br/>Leave blank to use a public container registry|
|IotHubConnectionString|Iot Hub connection string. "Device" our "Iot Hub owner" scopes are good.<br/> Example: `HostName=your-iothub-name.azure-devices.net;SharedAccessKeyName=device;SharedAccessKey=your-iothub-key`<br/>Only provide if messages are to be sent to IoT Hub.|
|EventHubConnectionString|Event Hub connection string. SAS Policy "Send" is required. <br/>For EventHub no device registration is required. Example:<br/> `Endpoint=sb://your-eventhub-namespace.servicebus.windows.net/;SharedAccessKeyName=send;SharedAccessKey=your-send-sas-primary-key;EntityPath=your-eventhub-name`<br/>Only provide if messages are to be sent to Event Hub.|
|PayloadDistribution|Allows the generation of payloads based on a distribution.<br/> Example: `"fixSize(10, 12) template(25, default) fix(65, aaaaBBBBBCCC)"`<br/> generates 10% a fix payload of 10 bytes, 25% a template generated payload<br/> and 65% of the time a fix payload from values aaaaBBBBBCCC|
|Template1|Telemetry payload template 1<br/>see [telemetry template](/README.md/#Telemetry-Template)|
|Template2|Telemetry payload template 2<br/>see [telemetry template](/README.md/#Telemetry-Template)|
|Variables|Telemetry variables<br/>see [telemetry template](/README.md/#Telemetry-Template)|
|DevicePrefix|Device identifier prefix (default = "sim<br/>Make sure these devices exist in your target IoT hub|
|Header|Telemetry header template|see [telemetry template](/README.md/#Telemetry-Template)|
|DeviceCount|Nubmer of simulated devices (default = 1)|
|MessageCount|Number of messages to send per device (default = 10).<br/> Set to zero if you wish to send messages until cancelled|If you run the automation component with 0, you need to manually stop each Azure Container Instance to stop the load test!|
|Interval|Interval between each message in milliseconds (default = 1000)|
|ContainerCount|Number of Azure Container Instances to be created<br/>See [sizing](#sizing)|
|CpuCores|Number of CPU Cores to allocate per Azure Container Instance<br/>See [sizing](#sizing)|
|MemoryGb|Gb of memory to allocate per Azure Container Instance<br/>See [sizing](#sizing)|

## Sizing

### CpuCores and MemoryGb

To find out the valid values for CPU cores and Gb of memory to allocate for the Azure Container Instances, please consult the [Resource availability for Azure Container Instances in Azure regions](https://docs.microsoft.com/bs-latn-ba/azure/container-instances/container-instances-region-availability?view=dotnet-uwp-10.0#availability---general) guide. The automation component uses **Linux** based containers.

It is usually not needed to use more than 1 CPU and 1.5 Gb of memory as the limitation of the container is the networking throughput, not the memory or CPU load.

### ContainerCount

The number of devices you plan to run your load test with is divided by the number of ConainerCount and each container will simulate this number of devices.

10'000 devices, 2 ContainerCount: 2 Azure Container Instances running with 5000 devices each.

Note that we have had the best results with up to 2500 messages per second sent from one container with a message payload size of about 500 bytes.