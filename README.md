# Azure Iot device telemetry simulator

IoT device simulator to test Azure IoT Hub ingest at scale. The implementation is communicating with Azure IoT Hub using multiplexed AMQP connections. A single AMQP connection can handle ~995 devices.

## Quick start

The quickest way to generate telemetry is using docker with the following command:

```cmd
docker run -it -e "IotHubConnectionString=HostName=your-iothub-name.azure-devices.net;SharedAccessKeyName=device;SharedAccessKey=your-iothub-key" fbeltrao/azureiot-telemetrysimulator
```

**The simulator expects the devices to already exist**. If you need help creating simulation devices in an Azure IoT Hub use the included project IotSimulatorDeviceProvisioning or the docker image:

```cmd
docker run -it -e "IotHubConnectionString=HostName=your-iothub-name.azure-devices.net;SharedAccessKeyName=device;SharedAccessKey=your-iothub-key" -e DeviceCount=1000 fbeltrao/azureiot-simulatordeviceprovisioning
```

## Simulator input parameters

The amount of devices, their names and telemetry generated can be customized using parameter. The list below contains the supported configuration parameters:

|Name|Description|
|-|-|
|IotHubConnectionString|Iot Hub connection string. "Device" our "Iot Hub owner" scopes are good. Example: HostName=your-iothub-name.azure-devices.net;SharedAccessKeyName=device;SharedAccessKey=your-iothub-key|
|DeviceList|comma separated list of device identifiers (default = ""). Use it to generate telemetry for specific devices instead of numeric generated identifiers. If the parameter has a value the following parameters are ignored: DevicePrefix, DeviceIndex and DeviceCount are ignored|
|DevicePrefix|device identifier prefix (default = "sim")|
|DeviceIndex|starting device number (default = 1)|
|DeviceCount|amount of simulated devices (default = 1)|
|MessageCount|amount of messages to send by device (default = 10). Set to zero if you wish to send messages until cancelled|
|Interval|interval between each message in milliseconds (default = 1000)|
|Template|telemetry payload template (see telemetry template)|
|Header|telemetry header template (see telemetry template)|
|Variables|telemetry variables (see telemetry template)|

## Telemetry template

The simulator is able to create user customizable telemetry with dynamic variables (random, counter, time, unique identifier, value range).

To generate a custom telemetry it is required to set the template and, optionally, variables.

The **template** defines how the telemetry looks like, having placeholders for variables.
Variables are declared in the telemetry as `$.VariableName`. The optional **header** defines properties that will be transmitted as message properties.

**Variables** are declared defining how values in the template will be resolved.

### Built-in variables

The following variables are provided out of the box:

|Name|Description|
|-|-|
|DeviceId|Outputs the device identifier|
|Guid|A unique identifier value|
|Time|Outputs the utc time in which the telemetry was generated in ISO 8601 format|
|LocalTime|Outputs the local time in which the telemetry was generated in ISO 8601 format|
|Ticks|Outputs the ticks in which the telemetry was generated|
|Epoch|Outputs the time in which the telemetry was generated in epoch format (seconds)|
|MachineName|Outputs the machine name where the generator is running (pod name if running in Kubernetes)|

### Customizable variables

Customizable variables can be created with the following properties:

|Name|Description|
|-|-|
|name|Name of the property. Defines what will be replaced in the template telemetry $.Name|
|random|Make the value random, limited by min and max|
|step|If the value is not random, will be incremented each time by the value of step|
|min|For random values defines it's minimum. Otherwise, will be the starting value|
|max|The maximum value generated|
|values|Defines an array of possible values. Example ["on", "off"]|

#### Example 1: Telemetry with temperature between 23 and 25 and a counter starting from 100

Template:

```json
{ "deviceId": "$.DeviceId", "temp": $.Temp, "Ticks": $.Ticks, "Counter": $.Counter, "time": "$.Time" }
```

Variables:

```json
[{name: "Temp", "random": true, "max": 25, "min": 23}, {"name":"Counter", "min":100}]
```

Output:

```json
{ "deviceId": "sim000001", "temp": 23, "Ticks": 637097550115091350, "Counter": 100, "time": "2019-11-19T10:10:11.5091350Z" }
{ "deviceId": "sim000001", "temp": 23, "Ticks": 637097550115952079, "Counter": 101, "time": "2019-11-19T10:10:11.5952079Z" }
{ "deviceId": "sim000001", "temp": 24, "Ticks": 637097550116627320, "Counter": 102, "time": "2019-11-19T10:10:11.6627320Z" }
```

Running with docker:

```
docker run -it -e "IotHubConnectionString=HostName=your-iothub-name.azure-devices.net;SharedAccessKeyName=device;SharedAccessKey=your-iothub-key" -e Template="{ \"deviceId\": \"$.DeviceId\", \"temp\": $.Temp, \"Ticks\": $.Ticks, \"Counter\": $.Counter, \"time\": \"$.Time\" }" -e Variables="[{name: \"Temp\", \"random\": true, \"max\": 25, \"min\": 23}, {\"name\":\"Counter\", \"min\":100} ]" fbeltrao/azureiot-telemetrysimulator
```

#### Example 2: Adding the engine status ("on" or "off") to the telemetry

Template:

```json
{ "deviceId": "$.DeviceId", "temp": $.Temp, "Ticks": $.Ticks, "Counter": $.Counter, "time": "$.Time", "engine": "$.Engine" }
```

Variables:

```json
[{name: "Temp", "random": true, "max": 25, "min": 23}, {"name":"Counter", "min":100}, {"name": "Engine", "values": ["on", "off"]}]
```

Output:

```json
{ "deviceId": "sim000001", "temp": 23, "Ticks": 637097644549666920, "Counter": 100, "time": "2019-11-19T12:47:34.9666920Z", "engine": "off" }
{ "deviceId": "sim000001", "temp": 24, "Ticks": 637097644550326096, "Counter": 101, "time": "2019-11-19T12:47:35.0326096Z", "engine": "on" }
```

Running with docker:

```
docker run -it -e "IotHubConnectionString=HostName=your-iothub-name.azure-devices.net;SharedAccessKeyName=device;SharedAccessKey=your-iothub-key" -e Template="{ \"deviceId\": \"$.DeviceId\", \"temp\": $.Temp, \"Ticks\": $.Ticks, \"Counter\": $.Counter, \"time\": \"$.Time\", \"engine\": \"$.Engine\" }" -e Variables="[{name: \"Temp\", \"random\": true, \"max\": 25, \"min\": 23}, {\"name\":\"Counter\", \"min\":100}, {name:\"Engine\", values: [\"on\", \"off\"]}]" fbeltrao/azureiot-telemetrysimulator
```

## Generating high volume of telemetry

In order to generate a constant high volume of messages a single computer might not be enough. This section describes two way to run the simulator to ingest a high volume of messages

### Azure Container Instance

Azure has container instances which allow the execution of containers with micro billing. This repository has a PowerShell script that creates azure container instances in your subscription. Requirements are having [az cli installed](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest).

To start the simulator in a single container instance:

```powershell
.\SimulatorCloudRunner.ps1
```

You will be asked to enter the Azure IoT Hub Connection string. After that, a resource group and one or more container instances will be created.

The cloud runner can be customized with the following parameters (as `-ParameterName ParameterValue`):

|Name|Description|
|-|-|
|Location|Location of the resource group being created. (Default = westeurope). For a list of locations try `az account list-locations -o table`|
|ResourceGroup|Resource group (will be created if it does not exist) where container instances will be created. (Default = iothubsimulator)|
|DeviceCount|Total amout of devices (Default = 100)|
|ContainerCount|Total amount of container instances to create. The total DeviceCount will be divided among all instances (Default = 1)|
|MessageCount|Total amount of messages to send per device. 0 means no limit, **causing the container to never end. It is your job to stop and delete it!** (Default = 100)|
|Interval|Interval in which each device will send messages in milliseconds (Default = 1000)|
|Template|Telemetry payload template to be used<br />(Default = '{ \"deviceId\": \"$.DeviceId\", \"temp\": $.Temp, \"Ticks\": $.Ticks, \"Counter\": $.Counter, \"time\": \"$.Time\", \"engine\": \"$.Engine\", \"source\": \"$.MachineName\" }')|
|Header|Header properties template to be used<br />(Default = '')|
|Variables|Variables used to create the telemetry<br />(Default = '[{name: \"Temp\", random: true, max: 25, min: 23}, {name:\"Counter\", min:100}, {name:\"Engine\", values: [\"on\", \"off\"]}]')|
|Cpu|Amount of cpus allocated to each container instance (Default = 1.0)|
|IotHubConnectionString|Azure Iot Hub connection string|

### Kubernetes

This repository also contains a helm chart to deploy the simulator to a Kubernetes cluster. An example release with helm for 5000 devices in 5 pods:

```bash
helm install sims iot-telemetry-simulator\. --namespace iotsimulator --set iotHubConnectionString="HostName=xxxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxxxxxx" --set replicaCount=5 --set deviceCount=5000
```
